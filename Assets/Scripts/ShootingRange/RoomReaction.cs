using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// Something in the room that does a thing when shot and puts itself back at the start of every round.
    /// Worth no points: it is there for fun, so it never touches the score or the combo
    public abstract class RoomReaction : MonoBehaviour, IShootable
    {
        private RangeGame _game;

        protected virtual void Start()
        {
            _game = RangeGame.Instance;
            if (_game != null) _game.RoundStarted += OnRoundStarted;
        }

        protected virtual void OnDestroy()
        {
            if (_game != null) _game.RoundStarted -= OnRoundStarted;
        }

        private void OnRoundStarted(int round) { Restore(); }

        public abstract void OnShot(Vector3 point, Vector3 direction, float impulse);

        protected abstract void Restore();

        protected static void PlayOne(AudioClip[] clips, Vector3 at, float volume)
        {
            if (clips == null || clips.Length == 0 || ImpactFX.Instance == null) return;
            ImpactFX.Instance.PlayClip(clips[Random.Range(0, clips.Length)], at, volume);
        }
    }
}
