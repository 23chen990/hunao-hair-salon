using System.Collections.Generic;
using HairSalon;
using UnityEngine;

public sealed class HaircutRuntimeFeedback : MonoBehaviour
{
    private sealed class Fragment
    {
        public Transform Transform;
        public Vector3 Velocity;
        public Vector3 Spin;
        public float Age;
        public bool Active;
    }

    private readonly List<Fragment> _fragments = new List<Fragment>();
    private HaircutConfig _config;
    private SalonCustomerView _view;
    private Transform _scissors;
    private Transform _thinningShears;
    private Transform _clippers;
    private Transform _activeToolRoot;
    private Transform _bladeA;
    private Transform _bladeB;
    private AudioSource _audioSource;
    private AudioClip _snipClip;
    private float _particleClock;
    private float _nextSnipTime;
    private bool _holding;
    private SalonTool _activeTool = SalonTool.Scissors;
    private Coroutine _wrongToolRoutine;

    public void Initialize(SalonCustomerView view, HaircutConfig config, Camera sceneCamera)
    {
        _view = view;
        _config = config;
        BuildScissors();
        BuildThinningShears();
        BuildClippers();
        BuildSnipAudio();
        BuildParticlePool();
    }

    public void BeginHold(SalonTool tool)
    {
        _activeTool = tool;
        SetActiveTool(tool);
        _holding = true;
        _particleClock = 0f;
        _nextSnipTime = 0f;
        _activeToolRoot.gameObject.SetActive(true);
    }

    public void TickHold(float holdTime, float progress)
    {
        if (!_holding) return;
        float cycle = _config.GetCycleSeconds(_activeTool);
        float open = .5f + .5f * Mathf.Sin(holdTime * Mathf.PI * 2f / cycle);
        if (_activeTool == SalonTool.Clippers)
        {
            float vibration = Mathf.Sin(holdTime * 85f) * .035f;
            _clippers.localPosition = new Vector3(.68f + vibration, 2.08f, -.58f);
            _clippers.localEulerAngles = new Vector3(0f, 0f, -18f + vibration * 100f);
        }
        else
        {
            float range = _activeTool == SalonTool.ThinningShears ? 16f : 24f;
            _bladeA.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-6f, range, open));
            _bladeB.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(6f, -range, open));
            _activeToolRoot.localPosition = new Vector3(.68f + Mathf.Sin(holdTime * 5f) * .05f, 2.12f, -.58f);
        }
        if (holdTime >= _nextSnipTime)
        {
            _audioSource.pitch = _activeTool == SalonTool.Clippers ? Random.Range(.42f, .5f) :
                                 _activeTool == SalonTool.ThinningShears ? Random.Range(1.12f, 1.22f) :
                                 Random.Range(.94f, 1.06f);
            _audioSource.PlayOneShot(_snipClip, _activeTool == SalonTool.Clippers ? .18f : .32f);
            _nextSnipTime += cycle;
        }

        float particleMultiplier = _activeTool == SalonTool.ThinningShears ? 1.35f : _activeTool == SalonTool.Clippers ? .7f : 1f;
        _particleClock += Time.deltaTime * Mathf.Max(0f, _config.ParticleRate) * particleMultiplier;
        while (_particleClock >= 1f)
        {
            SpawnFragment(false);
            _particleClock -= 1f;
        }
    }

    public void Stop(HaircutResult result)
    {
        _holding = false;
        HideTools();
        if (result == HaircutResult.Overcut || result == HaircutResult.WrongTool)
        {
            _audioSource.pitch = .78f;
            _audioSource.PlayOneShot(_snipClip, .6f);
            if (result == HaircutResult.Overcut)
                for (int i = 0; i < 8; i++) SpawnFragment(true);
        }
    }

    public void PlayImmediateResult(HaircutResult result, SalonTool tool)
    {
        _activeTool = tool;
        SetActiveTool(tool);
        _activeToolRoot.gameObject.SetActive(true);
        _audioSource.pitch = .68f;
        _audioSource.PlayOneShot(_snipClip, .55f);
        if (_wrongToolRoutine != null) StopCoroutine(_wrongToolRoutine);
        _wrongToolRoutine = StartCoroutine(WrongToolRoutine());
    }

    private System.Collections.IEnumerator WrongToolRoutine()
    {
        Vector3 origin = _activeToolRoot.localPosition;
        float elapsed = 0f;
        while (elapsed < .42f)
        {
            elapsed += Time.unscaledDeltaTime;
            float strength = 1f - Mathf.Clamp01(elapsed / .42f);
            _activeToolRoot.localPosition = origin + Vector3.right * Mathf.Sin(elapsed * 58f) * .16f * strength;
            _activeToolRoot.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(elapsed * 43f) * 22f * strength);
            yield return null;
        }
        _activeToolRoot.localPosition = origin;
        _activeToolRoot.gameObject.SetActive(false);
        _wrongToolRoutine = null;
    }

    public void Cancel()
    {
        if (_wrongToolRoutine != null)
        {
            StopCoroutine(_wrongToolRoutine);
            _wrongToolRoutine = null;
        }
        _holding = false;
        HideTools();
    }

    private void OnDisable()
    {
        Cancel();
    }

    private void Update()
    {
        for (int i = 0; i < _fragments.Count; i++)
        {
            var fragment = _fragments[i];
            if (!fragment.Active) continue;
            fragment.Age += Time.deltaTime;
            fragment.Velocity += Vector3.down * 3.8f * Time.deltaTime;
            fragment.Transform.localPosition += fragment.Velocity * Time.deltaTime;
            fragment.Transform.localEulerAngles += fragment.Spin * Time.deltaTime;
            float floorY = transform.InverseTransformPoint(new Vector3(transform.position.x, .04f, transform.position.z)).y;
            if (fragment.Transform.localPosition.y < floorY)
            {
                var position = fragment.Transform.localPosition;
                position.y = floorY;
                fragment.Transform.localPosition = position;
                fragment.Velocity = Vector3.zero;
            }
            if (fragment.Age >= Mathf.Max(.2f, _config.ParticleLife))
            {
                fragment.Active = false;
                fragment.Transform.gameObject.SetActive(false);
            }
        }
    }

    private void BuildScissors()
    {
        _scissors = new GameObject("剪刀操作占位动画").transform;
        _scissors.SetParent(transform, false);
        _scissors.localPosition = new Vector3(.68f, 2.12f, -.58f);
        _bladeA = CreatePart("Blade A", _scissors, new Vector3(.16f, 0f, 0f), new Vector3(.5f, .07f, .08f), SalonPalette.Metal).transform;
        _bladeB = CreatePart("Blade B", _scissors, new Vector3(.16f, 0f, .02f), new Vector3(.5f, .07f, .08f), SalonPalette.Metal).transform;
        CreatePart("Handle A", _scissors, new Vector3(-.18f, .12f, 0f), new Vector3(.22f, .12f, .1f), SalonPalette.Ink);
        CreatePart("Handle B", _scissors, new Vector3(-.18f, -.12f, 0f), new Vector3(.22f, .12f, .1f), SalonPalette.Ink);
        _scissors.gameObject.SetActive(false);
    }

    private void BuildThinningShears()
    {
        _thinningShears = new GameObject("分齿剪操作占位动画").transform;
        _thinningShears.SetParent(transform, false);
        _thinningShears.localPosition = new Vector3(.68f, 2.12f, -.58f);
        _bladeA = CreatePart("Thinning Blade A", _thinningShears, new Vector3(.16f, 0f, 0f), new Vector3(.5f, .055f, .08f), SalonPalette.Metal).transform;
        _bladeB = CreatePart("Thinning Blade B", _thinningShears, new Vector3(.16f, 0f, .02f), new Vector3(.5f, .055f, .08f), SalonPalette.Metal).transform;
        for (int i = 0; i < 5; i++)
            CreatePart("Tooth " + i, _thinningShears, new Vector3(.02f + i * .09f, .07f, -.01f), new Vector3(.035f, .12f, .09f), SalonPalette.Ink);
        CreatePart("Handle A", _thinningShears, new Vector3(-.18f, .12f, 0f), new Vector3(.22f, .11f, .1f), SalonPalette.Ink);
        CreatePart("Handle B", _thinningShears, new Vector3(-.18f, -.12f, 0f), new Vector3(.22f, .11f, .1f), SalonPalette.Ink);
        _thinningShears.gameObject.SetActive(false);
    }

    private void BuildClippers()
    {
        _clippers = new GameObject("推子操作占位动画").transform;
        _clippers.SetParent(transform, false);
        _clippers.localPosition = new Vector3(.68f, 2.08f, -.58f);
        _clippers.localEulerAngles = new Vector3(0f, 0f, -18f);
        CreatePart("Clipper Body", _clippers, Vector3.zero, new Vector3(.3f, .62f, .18f), SalonPalette.Danger);
        CreatePart("Clipper Grip", _clippers, new Vector3(0f, -.08f, -.1f), new Vector3(.2f, .32f, .08f), SalonPalette.Ink);
        CreatePart("Clipper Teeth", _clippers, new Vector3(0f, .36f, 0f), new Vector3(.42f, .12f, .2f), SalonPalette.Metal);
        _clippers.gameObject.SetActive(false);
    }

    private void SetActiveTool(SalonTool tool)
    {
        HideTools();
        _activeToolRoot = tool == SalonTool.ThinningShears ? _thinningShears :
                          tool == SalonTool.Clippers ? _clippers : _scissors;
        if (tool == SalonTool.Scissors)
        {
            _bladeA = _scissors.Find("Blade A");
            _bladeB = _scissors.Find("Blade B");
        }
        else if (tool == SalonTool.ThinningShears)
        {
            _bladeA = _thinningShears.Find("Thinning Blade A");
            _bladeB = _thinningShears.Find("Thinning Blade B");
        }
    }

    private void HideTools()
    {
        if (_scissors != null) _scissors.gameObject.SetActive(false);
        if (_thinningShears != null) _thinningShears.gameObject.SetActive(false);
        if (_clippers != null) _clippers.gameObject.SetActive(false);
    }

    private void BuildSnipAudio()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f;
        const int sampleRate = 22050;
        const int sampleCount = 1102;
        var samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = Mathf.Exp(-t * 58f);
            float metal = Mathf.Sin(t * Mathf.PI * 2f * 1180f) + .45f * Mathf.Sin(t * Mathf.PI * 2f * 1760f);
            samples[i] = metal * envelope * .34f;
        }
        _snipClip = AudioClip.Create("Procedural Scissor Snip Placeholder", sampleCount, 1, sampleRate, false);
        _snipClip.SetData(samples, 0);
    }

    private void BuildParticlePool()
    {
        int count = Mathf.Max(1, _config.MaxParticles);
        for (int i = 0; i < count; i++)
        {
            var go = CreatePart("Low Poly Hair Fragment " + i, transform, Vector3.zero, new Vector3(.12f, .08f, .1f), _view.HairColor);
            go.SetActive(false);
            _fragments.Add(new Fragment { Transform = go.transform });
        }
    }

    private void SpawnFragment(bool accidentBurst)
    {
        Fragment fragment = null;
        for (int i = 0; i < _fragments.Count; i++)
            if (!_fragments[i].Active) { fragment = _fragments[i]; break; }
        if (fragment == null) return;
        fragment.Active = true;
        fragment.Age = 0f;
        fragment.Transform.gameObject.SetActive(true);
        float size = _activeTool == SalonTool.ThinningShears ? .065f : _activeTool == SalonTool.Clippers ? .09f : .12f;
        fragment.Transform.localScale = new Vector3(size, size * .66f, size * .83f);
        fragment.Transform.localPosition = new Vector3(Random.Range(-.45f, .45f), Random.Range(1.95f, 2.35f), Random.Range(-.35f, .25f));
        float force = accidentBurst ? 1.55f : .85f;
        fragment.Velocity = new Vector3(Random.Range(-force, force), Random.Range(.5f, 1.35f) * force, Random.Range(-.45f, .45f));
        fragment.Spin = new Vector3(Random.Range(-180f, 180f), Random.Range(-180f, 180f), Random.Range(-180f, 180f));
    }

    private static GameObject CreatePart(string name, Transform parent, Vector3 position, Vector3 scale, Color color)
    {
        var go = SalonPrimitiveFactory.CreateCube(parent, position, scale, color);
        go.name = name;
        var collider = go.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;
        return go;
    }
}

public static class SalonPalette
{
    public static readonly Color Cream = Hex("FFF5E6");
    public static readonly Color Ink = Hex("2D292B");
    public static readonly Color Metal = Hex("D6D8D5");
    public static readonly Color Track = Hex("35423D");
    public static readonly Color Warning = Hex("F0AF36");
    public static readonly Color Success = Hex("58C56B");
    public static readonly Color Danger = Hex("E85143");

    private static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString("#" + value, out var color);
        return color;
    }
}
