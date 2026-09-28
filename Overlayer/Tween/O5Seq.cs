using System;
using System.Collections.Generic;
using O5Kit.Core;

namespace Overlayer.Tween;

/// <summary>Minimal done-callback tween sequence. Replaces the removed GTween sequences: Append runs after the previous group, Join runs in parallel with the current group.</summary>
public sealed class O5Seq : ITweenHandle {
    private readonly List<List<Action<Action>>> _groups = new();
    private Action _onComplete;
    private bool _loop;
    private bool _killed;
    private bool _playing;

    /// <summary>Creates an empty sequence. Add steps, then <see cref="Play"/>.</summary>
    public static O5Seq New() => new();

    /// <summary>Runs <paramref name="step"/> after all previous groups finish. The step must invoke its <c>done</c> callback.</summary>
    public O5Seq Append(Action<Action> step) {
        _groups.Add(new List<Action<Action>> { step });
        return this;
    }

    /// <summary>Runs <paramref name="step"/> in parallel with the current group. The step must invoke its <c>done</c> callback.</summary>
    public O5Seq Join(Action<Action> step) {
        if(_groups.Count == 0) {
            _groups.Add(new List<Action<Action>>());
        }

        _groups[_groups.Count - 1].Add(step);
        return this;
    }

    /// <summary>Runs <paramref name="cb"/> synchronously when reached, then continues.</summary>
    public O5Seq AppendCallback(Action cb) => Append(done => {
        try {
            cb();
        } finally {
            done();
        }
    });

    /// <summary>Invoked once when the last group finishes (unless killed or looping).</summary>
    public O5Seq OnComplete(Action onComplete) {
        _onComplete = onComplete;
        return this;
    }

    /// <summary>Restarts from the first group after the last one finishes, until killed.</summary>
    public O5Seq SetLoops() {
        _loop = true;
        return this;
    }

    /// <summary>Waits <paramref name="seconds"/> (unscaled) when reached, then continues.</summary>
    public O5Seq AppendTime(float seconds) => Append(done => {
        try {
            Overlayer.Compat.O5KitAdapters.Ctx.Tween.TweenFloat(
                () => 0f, _ => { }, 1f, Math.Max(seconds, 0.0001f), done);
        } catch {
            done();
        }
    });

    /// <summary>Starts the sequence. Returns this for handle storage.</summary>
    public O5Seq Play() {
        _killed = false;
        _playing = true;
        RunGroup(0);
        return this;
    }

    /// <summary>Whether the sequence is still running.</summary>
    public bool IsAlive => _playing && !_killed;

    /// <summary>Stops the sequence. Pending steps are ignored afterwards.</summary>
    /// <param name="complete">True to also fire the completion callback.</param>
    public void Kill(bool complete = false) {
        if(!_playing) {
            return;
        }

        _killed = true;
        _playing = false;

        if(complete && _onComplete != null) {
            try {
                _onComplete();
            } catch {
            }
        }
    }

    private void RunGroup(int index) {
        if(_killed) {
            return;
        }

        if(index >= _groups.Count) {
            if(_loop) {
                RunGroup(0);
                return;
            }

            _playing = false;

            if(_onComplete != null) {
                try {
                    _onComplete();
                } catch {
                }
            }

            return;
        }

        var group = _groups[index];
        if(group.Count == 0) {
            RunGroup(index + 1);
            return;
        }

        int pending = group.Count;
        foreach(var step in group) {
            Action done = () => {
                if(_killed) {
                    return;
                }

                if(--pending == 0) {
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
