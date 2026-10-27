using System.Collections.Generic;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange
{
    /// Something in the room that does a thing when shot and puts itself back at the start of every round.
    /// Worth no points: it is there for fun, so it never touches the score or the combo
    public abstract class RoomReaction : MonoBehaviour, IShootable
    {
        private RangeGame _game;

        private static readonly List<RoomReaction> All = new List<RoomReaction>();

        /// Something big went off here, a sign shattering: whatever is close enough gives a start
        public static void JoltAround(Vector3 at, float radius)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if ((All[i].transform.position - at).sqrMagnitude <= radius * radius) All[i].Jolt(at);
            }
        }

        /// Nothing by default: only things that can rock or turn give a start
        protected virtual void Jolt(Vector3 from) { }

        protected virtual void Start()
        {
            All.Add(this);
            _game = RangeGame.Instance;
            if (_game != null) _game.RoundStarted += OnRoundStarted;
        }

        protected virtual void OnDestroy()
        {
            All.Remove(this);
            if (_game != null) _game.RoundStarted -= OnRoundStarted;
        }

        private void OnRoundStarted(int round) { Restore(); }

        public abstract void OnShot(Vector3 point, Vector3 direction, float impulse);

        protected abstract void Restore();

        /// Whole and out of the way of rounds while protected: its colliders are off, so a shot goes straight through
        /// without a mark. The countdown skulls are protected while they count
        public virtual void SetProtected(bool on)
        {
            if (on) Restore();
            foreach (var item in GetComponentsInChildren<Collider>(true)) item.enabled = !on;
        }

        protected static void PlayOne(AudioClip[] clips, Vector3 at, float volume)
        {
            if (clips == null || clips.Length == 0 || ImpactFX.Instance == null) return;
            ImpactFX.Instance.PlayClip(clips[Random.Range(0, clips.Length)], at, volume);
        }
    }
}
