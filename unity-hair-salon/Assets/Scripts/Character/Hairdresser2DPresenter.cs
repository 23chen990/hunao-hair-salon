using System;
using UnityEngine;

namespace HairSalon.Character
{
    [Serializable]
    public sealed class HairdresserDirectionalSprites
    {
        [SerializeField] private Sprite[] directions = new Sprite[8];

        public Sprite Get(HairdresserDirection direction)
        {
            int index = (int)direction;
            return directions != null && index >= 0 && index < directions.Length
                ? directions[index]
                : null;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class Hairdresser2DPresenter : MonoBehaviour
    {
        [SerializeField] private HairdresserDirectionalSprites idle = new HairdresserDirectionalSprites();
        [SerializeField] private HairdresserDirectionalSprites walk = new HairdresserDirectionalSprites();
        [SerializeField] private HairdresserDirectionalSprites walkPassing = new HairdresserDirectionalSprites();
        [SerializeField] private HairdresserDirectionalSprites walkOpposite = new HairdresserDirectionalSprites();
        [SerializeField] private HairdresserDirectionalSprites cutHair = new HairdresserDirectionalSprites();
        [SerializeField] private HairdresserDirectionalSprites dryHair = new HairdresserDirectionalSprites();
        [SerializeField] private HairdresserDirectionalSprites washHair = new HairdresserDirectionalSprites();
        [SerializeField] private bool faceCamera = true;
        [SerializeField, Min(.1f)] private float walkCyclesPerSecond = 1.7f;
        [SerializeField, Min(0f)] private float locomotionBlendSpeed = 12f;
        [SerializeField, Min(0f)] private float gaitSway = .025f;
        [SerializeField, Min(0f)] private float gaitBob = .05f;
        [SerializeField, Min(0f)] private float gaitLeanDegrees = 2.2f;

        private SpriteRenderer _renderer;
        private Vector3 _restLocalPosition;
        private Vector3 _restLocalScale;
        private Quaternion _restLocalRotation;
        private float _walkPhase;
        private float _locomotionBlend;
        private int _walkFrame = -1;

        public HairdresserAnimationState CurrentState { get; private set; } = HairdresserAnimationState.Idle;
        public HairdresserDirection CurrentDirection { get; private set; } = HairdresserDirection.South;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _restLocalPosition = transform.localPosition;
            _restLocalScale = transform.localScale;
            _restLocalRotation = transform.localRotation;
            RefreshSprite();
        }

        private void LateUpdate()
        {
            float targetBlend = CurrentState == HairdresserAnimationState.Walk ? 1f : 0f;
            float blend = 1f - Mathf.Exp(-locomotionBlendSpeed * Time.deltaTime);
            _locomotionBlend = Mathf.Lerp(_locomotionBlend, targetBlend, blend);
            if (CurrentState == HairdresserAnimationState.Walk)
                _walkPhase = Mathf.Repeat(_walkPhase + Time.deltaTime * walkCyclesPerSecond, 1f);

            int frame = ResolveWalkFrame(_walkPhase);
            if (CurrentState == HairdresserAnimationState.Walk && frame != _walkFrame)
            {
                _walkFrame = frame;
                RefreshSprite();
            }

            Vector3 unitOffset = EvaluateGaitOffset(_walkPhase, _locomotionBlend);
            transform.localPosition = _restLocalPosition + new Vector3(
                unitOffset.x * (gaitSway / .025f),
                unitOffset.y * (gaitBob / .05f), 0f);
            float step = Mathf.Sin(_walkPhase * Mathf.PI * 2f) * _locomotionBlend;
            float compression = Mathf.Clamp01(unitOffset.y / .05f) * .012f;
            transform.localScale = Vector3.Scale(_restLocalScale,
                new Vector3(1f + compression, 1f - compression, 1f));
            Quaternion gaitRoll = Quaternion.Euler(0f, 0f, -step * gaitLeanDegrees);
            transform.localRotation = _restLocalRotation * gaitRoll;
            if (faceCamera && Camera.main != null)
                transform.rotation = Camera.main.transform.rotation * gaitRoll;
        }

        public void Apply(HairdresserAnimationState state, HairdresserDirection direction)
        {
            if (CurrentState != HairdresserAnimationState.Walk &&
                state == HairdresserAnimationState.Walk)
            {
                _walkPhase = 0f;
                _walkFrame = -1;
            }
            CurrentState = state;
            CurrentDirection = direction;
            RefreshSprite();
        }

        private void RefreshSprite()
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            HairdresserDirectionalSprites set = SetFor(CurrentState);
            Sprite sprite = set == null ? null : set.Get(CurrentDirection);
            if (sprite != null) _renderer.sprite = sprite;
        }

        public static int ResolveWalkFrame(float normalizedPhase)
        {
            int beat = Mathf.FloorToInt(Mathf.Repeat(normalizedPhase, 1f) * 4f);
            return beat == 0 ? 0 : beat == 2 ? 2 : 1;
        }

        public static Vector3 EvaluateGaitOffset(float normalizedPhase, float blend)
        {
            float weight = Mathf.Clamp01(blend);
            float phase = Mathf.Repeat(normalizedPhase, 1f) * Mathf.PI * 2f;
            float sway = Mathf.Sin(phase) * .025f * weight;
            float bob = (1f - Mathf.Cos(phase * 2f)) * .025f * weight;
            return new Vector3(sway, bob, 0f);
        }

        private HairdresserDirectionalSprites SetFor(HairdresserAnimationState state)
        {
            switch (state)
            {
                case HairdresserAnimationState.Walk:
                    if (_walkFrame == 1) return walkPassing;
                    if (_walkFrame == 2) return walkOpposite;
                    return walk;
                case HairdresserAnimationState.CutHair: return cutHair;
                case HairdresserAnimationState.DryHair: return dryHair;
                case HairdresserAnimationState.WashHair: return washHair;
                default: return idle;
            }
        }
    }
}
