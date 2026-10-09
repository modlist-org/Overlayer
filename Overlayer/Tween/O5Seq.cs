using O5Kit.Core;

namespace Overlayer.Tween;

/// <summary>DOTween is bad!!!</summary>
public sealed class O5Seq : ITweenHandle {
    private readonly List<List<Action<Action>>> _groups = new();
    private Action _onComplete;
    private bool _loop;
    private bool _killed;
    private bool _playing;

    public static O5Seq New() => new();

    public O5Seq Append(Action<Action> step) {
        _groups.Add(new List<Action<Action>> { step });
        return this;
    }

    public O5Seq Join(Action<Action> step) {
        if (_groups.Count == 0) {
            _groups.Add(new List<Action<Action>>());
        }

        _groups[_groups.Count - 1].Add(step);
        return this;
    }

    public O5Seq AppendCallback(Action cb) => Append(done => {
        try {
            cb();
        } finally {
            done();
        }
    });

    public O5Seq OnComplete(Action onComplete) {
        _onComplete = onComplete;
        return this;
    }

    public O5Seq SetLoops() {
        _loop = true;
        return this;
    }

    public O5Seq AppendTime(float seconds) => Append(done => {
        try {
            Overlayer.Compat.O5KitAdapters.Ctx.Tween.TweenFloat(
                () => 0f, _ => { }, 1f, Math.Max(seconds, 0.0001f), done);
        } catch {
            done();
        }
    });

    public O5Seq Play() {
        _killed = false;
        _playing = true;
        RunGroup(0);
        return this;
    }

    public bool IsAlive => _playing && !_killed;

    public void Kill(bool complete = false) {
        if (!_playing) {
            return;
        }

        _killed = true;
        _playing = false;

        if (complete && _onComplete != null) {
            try {
                _onComplete();
            } catch {
            }
        }
    }

    private void RunGroup(int index) {
        if (_killed) {
            return;
        }

        if (index >= _groups.Count) {
            if (_loop) {
                RunGroup(0);
                return;
            }

            _playing = false;

            if (_onComplete != null) {
                try {
                    _onComplete();
                } catch {
                }
            }

            return;
        }

        var group = _groups[index];
        if (group.Count == 0) {
            RunGroup(index + 1);
            return;
        }

        int pending = group.Count;
        foreach (var step in group) {
            Action done = () => {
                if (_killed) {
                    return;
                }

                if (--pending == 0) {
                    RunGroup(index + 1);
                }
            };

            try {
                step(done);
            } catch {
                done();
            }
        }
    }
}
