using System.Collections.Generic;
using HairSalon;
using UnityEngine;

public sealed partial class SalonDemo
{
    private bool _simple2DMode;
    private Texture2D _simple2DWhite;
    private GUIStyle _simple2DLabel;
    private GUIStyle _simple2DTitle;

    private void ConfigureSimple2DPresentation()
    {
        if (!_simple2DMode || _camera == null) return;

        _camera.orthographic = true;
        _camera.orthographicSize = 8.1f;
        _camera.backgroundColor = new Color(.08f, .10f, .14f);
        _camera.transform.position = new Vector3(0f, 20f, 1f);
        _camera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        _overviewCameraPosition = _camera.transform.position;
        _overviewCameraRotation = _camera.transform.rotation;
        _cameraPositionTarget = _overviewCameraPosition;
        _cameraSizeTarget = _camera.orthographicSize;

        Transform map = GameObject.Find("Fixed Salon Map")?.transform;
        if (map == null) return;
        foreach (Renderer renderer in map.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = false;
        foreach (Canvas canvas in map.GetComponentsInChildren<Canvas>(true))
            canvas.enabled = false;
        foreach (Light light in map.GetComponentsInChildren<Light>(true))
            light.enabled = false;
    }

    private void EnsureSimple2DStyles()
    {
        if (_simple2DWhite == null)
        {
            _simple2DWhite = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _simple2DWhite.name = "Simple 2D Greybox Pixel";
            _simple2DWhite.SetPixel(0, 0, Color.white);
            _simple2DWhite.Apply();
        }
        if (_simple2DLabel == null)
        {
            _simple2DLabel = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                wordWrap = false
            };
            _simple2DLabel.normal.textColor = Color.white;
            _simple2DTitle = new GUIStyle(_simple2DLabel)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
        }
    }

    private Rect Simple2DViewport()
    {
        float top = Mathf.Min(52f, Screen.height * .16f);
        float bottom = Mathf.Min(120f, Screen.height * .31f);
        return new Rect(12f, top, Mathf.Max(1f, Screen.width - 24f),
            Mathf.Max(1f, Screen.height - top - bottom));
    }

    private Vector2 Simple2DScreen(Vector3 world, Rect viewport)
    {
        Rect floor = _mobileFloor.width > .1f ? _mobileFloor : new Rect(-10.5f, -5.5f, 21f, 13f);
        float scale = Mathf.Min((viewport.width - 20f) / floor.width,
            (viewport.height - 20f) / floor.height);
        return new Vector2(viewport.xMin + 10f + (world.x - floor.xMin) * scale,
            viewport.yMax - 10f - (world.z - floor.yMin) * scale);
    }

    private void Simple2DRect(Rect viewport, Vector3 center, Vector2 size, Color color)
    {
        Rect floor = _mobileFloor.width > .1f ? _mobileFloor : new Rect(-10.5f, -5.5f, 21f, 13f);
        float scale = Mathf.Min((viewport.width - 20f) / floor.width,
            (viewport.height - 20f) / floor.height);
        Vector2 screen = Simple2DScreen(center, viewport);
        Rect rect = new Rect(screen.x - size.x * scale * .5f, screen.y - size.y * scale * .5f,
            size.x * scale, size.y * scale);
        GUI.color = color;
        GUI.DrawTexture(rect, _simple2DWhite);
    }

    private void Simple2DLabel(Rect viewport, Vector3 world, string text, Color color, Vector2 size)
    {
        Vector2 screen = Simple2DScreen(world, viewport);
        _simple2DLabel.normal.textColor = color;
        GUI.Label(new Rect(screen.x - size.x * .5f, screen.y - size.y * .5f, size.x, size.y),
            text ?? string.Empty, _simple2DLabel);
    }

    private void OnGUI()
    {
        if (!_simple2DMode || !_mobileMode || _game == null) return;
        EnsureSimple2DStyles();
        Rect viewport = Simple2DViewport();
        GUI.color = new Color(.06f, .08f, .11f, .98f);
        GUI.DrawTexture(viewport, _simple2DWhite);

        Rect floor = _mobileFloor.width > .1f ? _mobileFloor : new Rect(-10.5f, -5.5f, 21f, 13f);
        float scale = Mathf.Min((viewport.width - 20f) / floor.width,
            (viewport.height - 20f) / floor.height);
        for (int x = Mathf.FloorToInt(floor.xMin); x <= Mathf.CeilToInt(floor.xMax); x++)
        for (int z = Mathf.FloorToInt(floor.yMin); z <= Mathf.CeilToInt(floor.yMax); z++)
        {
            Color tile = ((x + z) & 1) == 0 ? new Color(.16f, .34f, .34f) : new Color(.14f, .30f, .31f);
            Simple2DRect(viewport, new Vector3(x + .5f, 0f, z + .5f), Vector2.one * .96f, tile);
        }
        foreach (Rect obstacle in _mobileObstacles)
            Simple2DRect(viewport, new Vector3(obstacle.center.x, 0f, obstacle.center.y),
                obstacle.size, new Color(.12f, .10f, .12f, .85f));

        foreach (KeyValuePair<int, Transform> pair in _playerServiceAnchors)
        {
            WorkstationType type = pair.Key >= 0 && pair.Key < _game.Workstations.Count
                ? _game.Workstations[pair.Key].Type : WorkstationType.Haircut;
            Color color = type == WorkstationType.Wash ? new Color(.20f, .62f, .62f) :
                type == WorkstationType.Haircut ? new Color(.86f, .46f, .35f) : new Color(.48f, .34f, .68f);
            Simple2DRect(viewport, pair.Value.position, new Vector2(1.65f, 1.15f), color);
            string label = type == WorkstationType.Wash ? "WASH" :
                type == WorkstationType.Haircut ? "CUT" : "PERM";
            Simple2DLabel(viewport, pair.Value.position + Vector3.back * .78f, label, Color.white, new Vector2(90f, 22f));
        }

        foreach (SalonCustomerView view in _customerViews)
        {
            if (view == null || view.Customer == null || !view.gameObject.activeSelf) continue;
            CustomerModel customer = view.Customer;
            Color color = customer.State == CustomerState.Waiting ? new Color(.86f, .40f, .64f) :
                customer.State == CustomerState.Serving ? new Color(.95f, .72f, .26f) :
                customer.State == CustomerState.Finished ? new Color(.42f, .80f, .44f) : Color.white;
            Simple2DRect(viewport, view.transform.position, new Vector2(.58f, .58f), color);
            string suffix = customer.IsComplete ? "✓" : customer.Patience <= 30f ? "!" : "";
            Simple2DLabel(viewport, view.transform.position + Vector3.forward * .52f,
                (customer.Id + 1).ToString() + suffix, Color.white, new Vector2(60f, 22f));
        }
        if (_player != null)
        {
            Simple2DRect(viewport, _player.position, new Vector2(.7f, .7f), new Color(.35f, .78f, .95f));
            Simple2DLabel(viewport, _player.position + Vector3.forward * .62f, "YOU", Color.white, new Vector2(70f, 22f));
        }

        GUI.color = Color.white;
        GUI.Label(new Rect(viewport.x + 10f, viewport.y + 8f, 260f, 28f), "2D GREYBOX  ·  SALON", _simple2DTitle);
        GUI.Label(new Rect(viewport.xMax - 215f, viewport.y + 10f, 205f, 24f),
            "MOVE  /  SERVE  /  REPEAT", _simple2DLabel);
        GUI.color = Color.white;
    }
}
