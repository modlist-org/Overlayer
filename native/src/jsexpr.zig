const std = @import("std");

// Numeric subset of JavaScript, evaluated with JavaScript semantics.
// Anything outside the subset (strings, objects, calls other than Math.*,
// boolean-typed results) fails to compile so the caller falls back to V8.

extern "c" fn sin(x: f64) f64;
extern "c" fn cos(x: f64) f64;
extern "c" fn tan(x: f64) f64;
extern "c" fn asin(x: f64) f64;
extern "c" fn acos(x: f64) f64;
extern "c" fn atan(x: f64) f64;
extern "c" fn atan2(y: f64, x: f64) f64;
extern "c" fn sinh(x: f64) f64;
extern "c" fn cosh(x: f64) f64;
extern "c" fn tanh(x: f64) f64;
extern "c" fn asinh(x: f64) f64;
extern "c" fn acosh(x: f64) f64;
extern "c" fn atanh(x: f64) f64;
extern "c" fn exp(x: f64) f64;
extern "c" fn expm1(x: f64) f64;
extern "c" fn log(x: f64) f64;
extern "c" fn log1p(x: f64) f64;
extern "c" fn log2(x: f64) f64;
extern "c" fn log10(x: f64) f64;
extern "c" fn cbrt(x: f64) f64;
extern "c" fn pow(x: f64, y: f64) f64;
extern "c" fn fmod(x: f64, y: f64) f64;

const Code = enum(u8) {
    num,
    load,
    load_t,
    neg,
    not,
    bnot,
    add,
    sub,
    mul,
    div,
    mod,
    pow,
    lt,
    le,
    gt,
    ge,
    eq,
    ne,
    drop2_false,
    drop2_true,
    land,
    lor,
    select,
    band,
    bor,
    bxor,
    shl,
    sar,
    shr,
    call1,
    call2,
    max,
    min,
    hypot,
};

const Fn = enum(u8) {
    abs, acos, acosh, asin, asinh, atan, atanh, cbrt, ceil, clz32, cos, cosh, exp, expm1, floor, fround,
    log, log1p, log10, log2, round, sign, sin, sinh, sqrt, tan, tanh, trunc,
    atan2, imul, pow,
};

const Op = struct {
    code: Code,
    n: u32 = 0,
    f: f64 = 0,
};

const max_ops = 1024;
const max_stack = 64;
const max_vars = 256;
const max_source = 4096;

const Builder = struct {
    var_count: u32 = 0,
    len: u32 = 0,
    ops: [max_ops]Op = undefined,
};

pub const Program = struct {
    var_count: u32,
    ops: []Op,
};

const Ty = enum { num, bool, mixed };

fn join(a: Ty, b: Ty) Ty {
    return if (a == b) a else .mixed;
}

const Error = error{Reject};

const Parser = struct {
    src: []const u16,
    pos: usize = 0,
    prog: *Builder,
    depth: u32 = 0,
    allow_t: bool = false,
    nest: u32 = 0,

    fn skip(p: *Parser) void {
        while (p.pos < p.src.len) : (p.pos += 1) {
            switch (p.src[p.pos]) {
                ' ', '\t', '\n', '\r' => {},
                else => return,
            }
        }
    }

    fn peekAt(p: *Parser, k: usize) u16 {
        return if (p.pos + k < p.src.len) p.src[p.pos + k] else 0;
    }

    // Matches an operator only when it is not the prefix of a longer one.
    fn eat(p: *Parser, comptime op: []const u8, comptime not_followed: []const u8) bool {
        p.skip();
        inline for (op, 0..) |c, i| if (p.peekAt(i) != c) return false;
        const next = p.peekAt(op.len);
        inline for (not_followed) |c| if (next == c) return false;
        p.pos += op.len;
        return true;
    }

    fn emit(p: *Parser, op: Op, pops: u32, pushes: u32) Error!void {
        if (p.prog.len == max_ops) return error.Reject;
        p.prog.ops[p.prog.len] = op;
        p.prog.len += 1;
        p.depth = p.depth - pops + pushes;
        if (p.depth > max_stack) return error.Reject;
    }

    fn binary(p: *Parser, code: Code) Error!void {
        try p.emit(.{ .code = code }, 2, 1);
    }

    fn expression(p: *Parser) Error!Ty {
        p.nest += 1;
        if (p.nest > 64) return error.Reject;
        defer p.nest -= 1;
        const cond = try p.logicalOr();
        if (!p.eat("?", "?.")) return cond;
        const a = try p.expression();
        if (!p.eat(":", "")) return error.Reject;
        const b = try p.expression();
        try p.emit(.{ .code = .select }, 3, 1);
        return join(a, b);
    }

    fn logicalOr(p: *Parser) Error!Ty {
        var ty = try p.logicalAnd();
        while (p.eat("||", "=")) {
            ty = join(ty, try p.logicalAnd());
            try p.binary(.lor);
        }
        p.skip();
        if (p.peekAt(0) == '?' and p.peekAt(1) == '?') return error.Reject;
        return ty;
    }

    fn logicalAnd(p: *Parser) Error!Ty {
        var ty = try p.bitOr();
        while (p.eat("&&", "=")) {
            ty = join(ty, try p.bitOr());
            try p.binary(.land);
        }
        return ty;
    }

    fn bitOr(p: *Parser) Error!Ty {
        var ty = try p.bitXor();
        while (p.eat("|", "|=")) {
            _ = try p.bitXor();
            try p.binary(.bor);
            ty = .num;
        }
        return ty;
    }

    fn bitXor(p: *Parser) Error!Ty {
        var ty = try p.bitAnd();
        while (p.eat("^", "=")) {
            _ = try p.bitAnd();
            try p.binary(.bxor);
            ty = .num;
        }
        return ty;
    }

    fn bitAnd(p: *Parser) Error!Ty {
        var ty = try p.equality();
        while (p.eat("&", "&=")) {
            _ = try p.equality();
            try p.binary(.band);
            ty = .num;
        }
        return ty;
    }

    fn equality(p: *Parser) Error!Ty {
        var ty = try p.relational();
        while (true) {
            const strict_eq = p.eat("===", "");
            const strict_ne = !strict_eq and p.eat("!==", "");
            const loose_eq = !strict_eq and !strict_ne and p.eat("==", "=");
            const loose_ne = !strict_eq and !strict_ne and !loose_eq and p.eat("!=", "=");
            if (!(strict_eq or strict_ne or loose_eq or loose_ne)) return ty;
            const rhs = try p.relational();
            if (strict_eq or strict_ne) {
                if (ty == .mixed or rhs == .mixed) return error.Reject;
                if (ty != rhs) {
                    try p.binary(if (strict_eq) .drop2_false else .drop2_true);
                    ty = .bool;
                    continue;
                }
            }
            try p.binary(if (strict_eq or loose_eq) .eq else .ne);
            ty = .bool;
        }
    }

    fn relational(p: *Parser) Error!Ty {
        var ty = try p.shift();
        while (true) {
            const code: Code = if (p.eat("<=", "")) .le else if (p.eat(">=", "")) .ge else if (p.eat("<", "<=")) .lt else if (p.eat(">", ">=")) .gt else return ty;
            _ = try p.shift();
            try p.binary(code);
            ty = .bool;
        }
    }

    fn shift(p: *Parser) Error!Ty {
        var ty = try p.additive();
        while (true) {
            const code: Code = if (p.eat(">>>", "=")) .shr else if (p.eat(">>", ">=")) .sar else if (p.eat("<<", "=")) .shl else return ty;
            _ = try p.additive();
            try p.binary(code);
            ty = .num;
        }
    }

    fn additive(p: *Parser) Error!Ty {
        var ty = try p.multiplicative();
        while (true) {
            const code: Code = if (p.eat("+", "+=")) .add else if (p.eat("-", "-=")) .sub else return ty;
            _ = try p.multiplicative();
            try p.binary(code);
            ty = .num;
        }
    }

    fn multiplicative(p: *Parser) Error!Ty {
        var ty = try p.exponent();
        while (true) {
            const code: Code = if (p.eat("*", "*=")) .mul else if (p.eat("/", "/=*")) .div else if (p.eat("%", "=")) .mod else return ty;
            _ = try p.exponent();
            try p.binary(code);
            ty = .num;
        }
    }

    // JS forbids a bare unary operand on the left of `**` (`-2 ** 2`).
    fn exponent(p: *Parser) Error!Ty {
        if (try p.unaryPrefix()) |ty| {
            if (p.eat("**", "=")) return error.Reject;
            return ty;
        }
        const ty = try p.postfix();
        if (!p.eat("**", "=")) return ty;
        _ = try p.exponent();
        try p.binary(.pow);
        return .num;
    }

    fn unaryPrefix(p: *Parser) Error!?Ty {
        if (p.eat("!", "=")) {
            _ = try p.unaryOperand();
            try p.emit(.{ .code = .not }, 1, 1);
            return .bool;
        }
        if (p.eat("~", "")) {
            _ = try p.unaryOperand();
            try p.emit(.{ .code = .bnot }, 1, 1);
            return .num;
        }
        if (p.eat("-", "-=")) {
            _ = try p.unaryOperand();
            try p.emit(.{ .code = .neg }, 1, 1);
            return .num;
        }
        if (p.eat("+", "+=")) {
            _ = try p.unaryOperand();
            return .num;
        }
        return null;
    }

    fn unaryOperand(p: *Parser) Error!Ty {
        p.nest += 1;
        if (p.nest > 64) return error.Reject;
        defer p.nest -= 1;
        if (try p.unaryPrefix()) |ty| return ty;
        return p.postfix();
    }

    fn postfix(p: *Parser) Error!Ty {
        const ty = try p.primary();
        p.skip();
        // Member access, calls, indexing, optional chaining, templates, ++/--.
        switch (p.peekAt(0)) {
            '.', '(', '[', '`' => return error.Reject,
            '?' => if (p.peekAt(1) == '.') return error.Reject,
            '+' => if (p.peekAt(1) == '+') return error.Reject,
            '-' => if (p.peekAt(1) == '-') return error.Reject,
            else => {},
        }
        return ty;
    }

    fn ident(p: *Parser) []const u16 {
        p.skip();
        const start = p.pos;
        while (p.pos < p.src.len and isIdentChar(p.src[p.pos])) p.pos += 1;
        return p.src[start..p.pos];
    }

    fn primary(p: *Parser) Error!Ty {
        p.skip();
        if (p.pos >= p.src.len) return error.Reject;
        const c = p.src[p.pos];
        if (c == '(') {
            p.pos += 1;
            const ty = try p.expression();
            if (!p.eat(")", "")) return error.Reject;
            return ty;
        }
        if (isDigit(c) or (c == '.' and isDigit(p.peekAt(1)))) {
            try p.emit(.{ .code = .num, .f = try p.number() }, 0, 1);
            return .num;
        }
        if (!isIdentStart(c)) return error.Reject;
        const name = p.ident();
        if (eql(name, "true") or eql(name, "false")) {
            try p.emit(.{ .code = .num, .f = if (eql(name, "true")) 1 else 0 }, 0, 1);
            return .bool;
        }
        if (eql(name, "Infinity")) return p.constant(std.math.inf(f64));
        if (eql(name, "NaN")) return p.constant(std.math.nan(f64));
        if (eql(name, "Math")) return p.math();
        if (p.allow_t and eql(name, "t")) {
            try p.emit(.{ .code = .load_t }, 0, 1);
            return .num;
        }
        if (name.len >= 2 and name[0] == 'v' and (name.len == 2 or name[1] != '0')) {
            var index: u32 = 0;
            for (name[1..]) |d| {
                if (!isDigit(d)) return error.Reject;
                index = index * 10 + (d - '0');
                if (index >= max_vars) return error.Reject;
            }
            p.prog.var_count = @max(p.prog.var_count, index + 1);
            try p.emit(.{ .code = .load, .n = index }, 0, 1);
            return .num;
        }
        return error.Reject;
    }

    fn constant(p: *Parser, value: f64) Error!Ty {
        try p.emit(.{ .code = .num, .f = value }, 0, 1);
        return .num;
    }

    fn math(p: *Parser) Error!Ty {
        if (!p.eat(".", "")) return error.Reject;
        const name = p.ident();
        inline for (.{
            .{ "PI", std.math.pi },          .{ "E", std.math.e },
            .{ "LN2", std.math.ln2 },        .{ "LN10", std.math.ln10 },
            .{ "LOG2E", std.math.log2e },    .{ "LOG10E", std.math.log10e },
            .{ "SQRT2", std.math.sqrt2 },    .{ "SQRT1_2", std.math.sqrt1_2 },
        }) |entry| {
            if (eql(name, entry[0])) return p.constant(entry[1]);
        }
        if (!p.eat("(", "")) return error.Reject;
        var argc: u32 = 0;
        p.skip();
        if (p.peekAt(0) != ')') {
            while (true) {
                _ = try p.expression();
                argc += 1;
                if (!p.eat(",", "")) break;
                p.skip();
                if (p.peekAt(0) == ')') break;
            }
        }
        if (!p.eat(")", "")) return error.Reject;
        inline for (.{ .{ "max", Code.max }, .{ "min", Code.min }, .{ "hypot", Code.hypot } }) |entry| {
            if (eql(name, entry[0])) {
                try p.emit(.{ .code = entry[1], .n = argc }, argc, 1);
                return .num;
            }
        }
        inline for (@typeInfo(Fn).@"enum".field_names, @typeInfo(Fn).@"enum".field_values) |fname, fvalue| {
            if (eql(name, fname)) {
                const f: Fn = @enumFromInt(fvalue);
                const arity: u32 = if (@intFromEnum(f) >= @intFromEnum(Fn.atan2)) 2 else 1;
                if (argc != arity) return error.Reject;
                try p.emit(.{ .code = if (arity == 1) .call1 else .call2, .n = fvalue }, arity, 1);
                return .num;
            }
        }
        return error.Reject;
    }

    fn number(p: *Parser) Error!f64 {
        const start = p.pos;
        if (p.src[p.pos] == '0' and p.pos + 1 < p.src.len) {
            const base: u8 = switch (p.src[p.pos + 1] | 0x20) {
                'x' => 16,
                'b' => 2,
                'o' => 8,
                else => 0,
            };
            if (base != 0) {
                p.pos += 2;
                var value: u64 = 0;
                var digits: usize = 0;
                while (p.pos < p.src.len) : (p.pos += 1) {
                    const d = std.fmt.charToDigit(std.math.cast(u8, p.src[p.pos]) orelse break, base) catch break;
                    value = value * base + d;
                    digits += 1;
                    if (value >= 1 << 53) return error.Reject;
                }
                if (digits == 0 or (p.pos < p.src.len and isIdentChar(p.src[p.pos]))) return error.Reject;
                return @floatFromInt(value);
            }
            // Legacy octal (017) and separators are left to V8.
            if (isDigit(p.src[p.pos + 1]) or p.src[p.pos + 1] == '_') return error.Reject;
        }
        var buf: [64]u8 = undefined;
        var n: usize = 0;
        var seen_dot = false;
        var seen_exp = false;
        while (p.pos < p.src.len) : (p.pos += 1) {
            const c = p.src[p.pos];
            const ok = isDigit(c) or
                (c == '.' and !seen_dot and !seen_exp) or
                ((c | 0x20) == 'e' and !seen_exp and n > 0) or
                ((c == '+' or c == '-') and n > 0 and (buf[n - 1] | 0x20) == 'e');
            if (!ok) break;
            if (c == '.') seen_dot = true;
            if ((c | 0x20) == 'e') seen_exp = true;
            if (n == buf.len) return error.Reject;
            buf[n] = @intCast(c);
            n += 1;
        }
        if (p.pos < p.src.len and isIdentChar(p.src[p.pos])) return error.Reject;
        if (p.pos == start or (buf[n - 1] | 0x20) == 'e' or buf[n - 1] == '+' or buf[n - 1] == '-') return error.Reject;
        return std.fmt.parseFloat(f64, buf[0..n]) catch error.Reject;
    }
};

fn isDigit(c: u16) bool {
    return c >= '0' and c <= '9';
}

fn isIdentStart(c: u16) bool {
    return (c >= 'a' and c <= 'z') or (c >= 'A' and c <= 'Z') or c == '_' or c == '$';
}

fn isIdentChar(c: u16) bool {
    return isIdentStart(c) or isDigit(c) or c >= 0x80;
}

fn eql(a: []const u16, comptime b: []const u8) bool {
    if (a.len != b.len) return false;
    inline for (b, 0..) |c, i| if (a[i] != c) return false;
    return true;
}

// `allow_t` binds `t` as a parameter, as in V8's `(function(t){ return (code); })`.
pub fn compile(src: []const u16, allow_t: bool) ?*Program {
    if (src.len == 0 or src.len > max_source) return null;
    var builder = Builder{};
    var p = Parser{ .src = src, .prog = &builder, .allow_t = allow_t };
    const ty = p.expression() catch return null;
    p.skip();
    if (p.pos != src.len or ty != .num) return null;
    const allocator = std.heap.c_allocator;
    const prog = allocator.create(Program) catch return null;
    prog.* = .{
        .var_count = builder.var_count,
        .ops = allocator.dupe(Op, builder.ops[0..builder.len]) catch {
            allocator.destroy(prog);
            return null;
        },
    };
    return prog;
}

pub fn destroy(prog: *Program) void {
    std.heap.c_allocator.free(prog.ops);
    std.heap.c_allocator.destroy(prog);
}

fn truthy(x: f64) bool {
    return x != 0 and !std.math.isNan(x);
}

fn toUint32(x: f64) u32 {
    if (!std.math.isFinite(x)) return 0;
    const m = fmod(@trunc(x), 4294967296.0);
    return @intFromFloat(if (m < 0) m + 4294967296.0 else m);
}

fn toInt32(x: f64) i32 {
    return @bitCast(toUint32(x));
}

fn jsPow(x: f64, y: f64) f64 {
    if (std.math.isNan(y)) return std.math.nan(f64);
    if (@abs(x) == 1 and std.math.isInf(y)) return std.math.nan(f64);
    return pow(x, y);
}

fn jsRound(x: f64) f64 {
    if (!std.math.isFinite(x) or x == 0) return x;
    if (x > 0 and x < 0.5) return 0;
    if (x < 0 and x >= -0.5) return -0.0;
    const r = @floor(x);
    return if (x - r >= 0.5) r + 1 else r;
}

fn jsSign(x: f64) f64 {
    return if (x > 0) 1 else if (x < 0) -1 else x;
}

fn call1(f: Fn, x: f64) f64 {
    return switch (f) {
        .abs => @abs(x),
        .acos => acos(x),
        .acosh => acosh(x),
        .asin => asin(x),
        .asinh => asinh(x),
        .atan => atan(x),
        .atanh => atanh(x),
        .cbrt => cbrt(x),
        .ceil => @ceil(x),
        .clz32 => @floatFromInt(@clz(toUint32(x))),
        .cos => cos(x),
        .cosh => cosh(x),
        .exp => exp(x),
        .expm1 => expm1(x),
        .floor => @floor(x),
        .fround => @as(f32, @floatCast(x)),
        .log => log(x),
        .log1p => log1p(x),
        .log10 => log10(x),
        .log2 => log2(x),
        .round => jsRound(x),
        .sign => jsSign(x),
        .sin => sin(x),
        .sinh => sinh(x),
        .sqrt => @sqrt(x),
        .tan => tan(x),
        .tanh => tanh(x),
        .trunc => @trunc(x),
        else => unreachable,
    };
}

fn call2(f: Fn, a: f64, b: f64) f64 {
    return switch (f) {
        .atan2 => atan2(a, b),
        .imul => @floatFromInt(toInt32(a) *% toInt32(b)),
        .pow => jsPow(a, b),
        else => unreachable,
    };
}

fn extreme(args: []const f64, comptime want_max: bool) f64 {
    var r: f64 = if (want_max) -std.math.inf(f64) else std.math.inf(f64);
    var nan = false;
    for (args) |v| {
        if (std.math.isNan(v)) {
            nan = true;
        } else if (if (want_max) v > r else v < r) {
            r = v;
        } else if (v == 0 and r == 0 and std.math.signbit(v) != want_max) {
            r = v;
        }
    }
    return if (nan) std.math.nan(f64) else r;
}

// Same algorithm as V8's MathHypot builtin so results match bit for bit.
fn hypot(args: []const f64) f64 {
    var max: f64 = 0;
    var nan = false;
    for (args) |v| {
        const a = @abs(v);
        if (std.math.isNan(a)) nan = true else if (a > max) max = a;
    }
    if (std.math.isInf(max)) return max;
    if (nan) return std.math.nan(f64);
    if (max == 0) return 0;
    var sum: f64 = 0;
    var compensation: f64 = 0;
    for (args) |v| {
        const n = @abs(v) / max;
        const summand = n * n - compensation;
        const preliminary = sum + summand;
        compensation = (preliminary - sum) - summand;
        sum = preliminary;
    }
    return @sqrt(sum) * max;
}

pub fn eval(prog: *const Program, t: f64, vars: []const f64) f64 {
    if (vars.len < prog.var_count) return std.math.nan(f64);
    var stack: [max_stack]f64 = undefined;
    var sp: usize = 0;
    for (prog.ops) |op| {
        switch (op.code) {
            .num => {
                stack[sp] = op.f;
                sp += 1;
            },
            .load => {
                stack[sp] = vars[op.n];
                sp += 1;
            },
            .load_t => {
                stack[sp] = t;
                sp += 1;
            },
            .neg => stack[sp - 1] = -stack[sp - 1],
            .not => stack[sp - 1] = if (truthy(stack[sp - 1])) 0 else 1,
            .bnot => stack[sp - 1] = @floatFromInt(~toInt32(stack[sp - 1])),
            .select => {
                sp -= 2;
                stack[sp - 1] = if (truthy(stack[sp - 1])) stack[sp] else stack[sp + 1];
            },
            .call1 => stack[sp - 1] = call1(@enumFromInt(op.n), stack[sp - 1]),
            .max, .min, .hypot => {
                sp -= op.n;
                const args = stack[sp..][0..op.n];
                stack[sp] = switch (op.code) {
                    .max => extreme(args, true),
                    .min => extreme(args, false),
                    else => hypot(args),
                };
                sp += 1;
            },
            else => {
                sp -= 1;
                const a = stack[sp - 1];
                const b = stack[sp];
                stack[sp - 1] = switch (op.code) {
                    .add => a + b,
                    .sub => a - b,
                    .mul => a * b,
                    .div => a / b,
                    .mod => fmod(a, b),
                    .pow => jsPow(a, b),
                    .lt => @floatFromInt(@intFromBool(a < b)),
                    .le => @floatFromInt(@intFromBool(a <= b)),
                    .gt => @floatFromInt(@intFromBool(a > b)),
                    .ge => @floatFromInt(@intFromBool(a >= b)),
                    .eq => @floatFromInt(@intFromBool(a == b)),
                    .ne => @floatFromInt(@intFromBool(a != b)),
                    .drop2_false => 0,
                    .drop2_true => 1,
                    .land => if (truthy(a)) b else a,
                    .lor => if (truthy(a)) a else b,
                    .band => @floatFromInt(toInt32(a) & toInt32(b)),
                    .bor => @floatFromInt(toInt32(a) | toInt32(b)),
                    .bxor => @floatFromInt(toInt32(a) ^ toInt32(b)),
                    .shl => @floatFromInt(@as(i32, @bitCast(toUint32(a) << @intCast(toUint32(b) & 31)))),
                    .sar => @floatFromInt(toInt32(a) >> @intCast(toUint32(b) & 31)),
                    .shr => @floatFromInt(toUint32(a) >> @intCast(toUint32(b) & 31)),
                    .call2 => call2(@enumFromInt(op.n), a, b),
                    else => unreachable,
                };
            },
        }
    }
    return stack[0];
}

fn run(comptime source: []const u8, vars: []const f64) !f64 {
    const prog = compile(std.unicode.utf8ToUtf16LeStringLiteral(source), false) orelse return error.Rejected;
    defer destroy(prog);
    return eval(prog, 0, vars);
}

fn expectJs(comptime source: []const u8, expected: f64) !void {
    const got = try run(source, &.{ 3, -2.5, 0.5 });
    if (std.math.isNan(expected)) return std.testing.expect(std.math.isNan(got));
    try std.testing.expectEqual(@as(u64, @bitCast(expected)), @as(u64, @bitCast(got)));
}

fn expectReject(comptime source: []const u8) !void {
    try std.testing.expect(compile(std.unicode.utf8ToUtf16LeStringLiteral(source), false) == null);
}

test "arithmetic and precedence" {
    try expectJs("1 + 2 * 3", 7);
    try expectJs("(1 + 2) * 3", 9);
    try expectJs("2 ** 3 ** 2", 512);
    try expectJs("2 ** -1", 0.5);
    try expectJs("(-2) ** 2", 4);
    try expectJs("7 / 2", 3.5);
    try expectJs("-7 % 3", -1);
    try expectJs("7 % -3", 1);
    try expectJs("5 % 0", std.math.nan(f64));
    try expectJs("1 / 0", std.math.inf(f64));
    try expectJs("-1 / 0", -std.math.inf(f64));
    try expectJs("1 ** NaN", std.math.nan(f64));
    try expectJs("1 ** Infinity", std.math.nan(f64));
    try expectJs("v0 * 2 + v1", 3.5);
    try expectJs(".5e1 + 0x1F + 0b11 + 0o7", 5 + 31 + 3 + 7);
    try expectJs("1e3", 1000);
    try expectJs("- -3", 3);
    try expectJs("+true + true", 2);
}

test "bitwise uses int32" {
    try expectJs("1 << 40", 256);
    try expectJs("-1 >> 1", -1);
    try expectJs("-1 >>> 0", 4294967295);
    try expectJs("~5", -6);
    try expectJs("~~-3.7", -3);
    try expectJs("4294967297 | 0", 1);
    try expectJs("2147483648 | 0", -2147483648);
    try expectJs("NaN | 0", 0);
    try expectJs("5 ^ 3", 6);
    try expectJs("1 << 31", -2147483648);
}

test "logic ternary and comparisons" {
    try expectJs("v0 > 2 ? 10 : 20", 10);
    try expectJs("0 || 5", 5);
    try expectJs("NaN || 7", 7);
    try expectJs("3 && 4", 4);
    try expectJs("0 && 4", 0);
    try expectJs("(1 < 2) + (2 < 1)", 1);
    try expectJs("(1 === true) + (1 == true) + (1 !== true)", 2);
    try expectJs("!0 + !5", 1);
    try expectJs("NaN == NaN ? 1 : 2", 2);
    try expectJs("NaN != NaN ? 1 : 2", 1);
    try expectJs("1 ? 2 : 3 ? 4 : 5", 2);
}

test "Math matches JS edge cases" {
    try expectJs("Math.round(2.5)", 3);
    try expectJs("Math.round(-2.5)", -2);
    try expectJs("Math.round(-0.4)", -0.0);
    try expectJs("Math.round(0.49999999999999994)", 0);
    try expectJs("Math.max()", -std.math.inf(f64));
    try expectJs("Math.min()", std.math.inf(f64));
    try expectJs("Math.max(1, v0, 2)", 3);
    try expectJs("Math.max(-0, 0)", 0);
    try expectJs("Math.min(0, -0)", -0.0);
    try expectJs("Math.max(1, NaN)", std.math.nan(f64));
    try expectJs("Math.hypot(3, 4)", 5);
    try expectJs("Math.hypot()", 0);
    try expectJs("Math.hypot(NaN, Infinity)", std.math.inf(f64));
    try expectJs("Math.sign(-0)", -0.0);
    try expectJs("Math.ceil(-0.5)", -0.0);
    try expectJs("Math.trunc(-4.7)", -4);
    try expectJs("Math.abs(v1)", 2.5);
    try expectJs("Math.imul(0xffffffff, 5)", -5);
    try expectJs("Math.clz32(1)", 31);
    try expectJs("Math.clz32(0)", 32);
    try expectJs("Math.fround(5.5)", 5.5);
    try expectJs("Math.pow(2, 10)", 1024);
    try expectJs("Math.PI", std.math.pi);
    try expectJs("Math.sqrt(16) + Math.floor(v2)", 4);
}

test "everything else is left to V8" {
    try expectReject("1 < 2");
    try expectReject("true");
    try expectReject("v0 > 1 ? true : 0");
    try expectReject("'a' + 1");
    try expectReject("Math.random()");
    try expectReject("Math.round(1, 2)");
    try expectReject("Math.max");
    try expectReject("foo(1)");
    try expectReject("t + 1");
    try expectReject("-2 ** 2");
    try expectReject("2 ** -2 ** 2");
    try expectReject("x = 3");
    try expectReject("v0 += 1");
    try expectReject("v0++");
    try expectReject("017");
    try expectReject("1_000");
    try expectReject("1 ?? 2");
    try expectReject("v0.toFixed(2)");
    try expectReject("(1, 2)");
    try expectReject("1 /* c */ + 2");
    try expectReject("1 // c");
    try expectReject("v01");
    try expectReject("v256");
    try expectReject("1 === (1 && true)");
    try expectReject("");
    try expectReject("(");
    try expectReject("1 +");
    try expectReject("1e");
    try expectReject("3in");
}

test "missing variables yield NaN and deep nesting is rejected" {
    const prog = compile(std.unicode.utf8ToUtf16LeStringLiteral("v3 + 1"), false).?;
    defer destroy(prog);
    try std.testing.expect(std.math.isNan(eval(prog, 0, &.{ 1, 2 })));
    try expectReject("((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((1))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))))");
}

test "t is a parameter only when allowed" {
    const prog = compile(std.unicode.utf8ToUtf16LeStringLiteral("Math.sin(t) * 0 + t * v0"), true).?;
    defer destroy(prog);
    try std.testing.expectEqual(@as(f64, 6), eval(prog, 2, &.{3}));
    try expectReject("t");
}
