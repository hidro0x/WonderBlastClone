using System;
using UnityEngine;

public class InputHandler : MonoBehaviour
{
    private Camera _mainCamera;
    public static Action<Tile> OnTileClicked;

    private bool _clickable;
    public static Action<bool> OnControlInput;

    private void Awake()
    {
        _mainCamera = Camera.main;

        OnControlInput += HandleInteract;
        _clickable = true;
    }

    private void OnDisable()
    {
        OnControlInput -= HandleInteract;
    }

    private void HandleInteract(bool b) => _clickable = b;

    void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        HandleStandaloneInput();
#elif UNITY_ANDROID
        HandleMobileInput();
#endif
    }

    private void HandleMobileInput()
    {
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            ProcessInput(Input.GetTouch(0).position);
        }
    }

    private void HandleStandaloneInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            ProcessInput(Input.mousePosition);
        }
    }

    private void ProcessInput(Vector3 screenPosition)
    {
        if (!_clickable) return;
        Vector3 worldPosition = _mainCamera.ScreenToWorldPoint(screenPosition);
        Vector2 rayOrigin = new Vector2(worldPosition.x, worldPosition.y);

        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.zero);


        if (hit.collider != null)
        {
            Tile tileComponent = hit.transform.GetComponent<Tile>();
            if (tileComponent != null)
            {
                OnTileClicked?.Invoke(tileComponent);
            }
        }
    }
}