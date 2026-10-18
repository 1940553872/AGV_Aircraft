using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Spins the sphere continuously and toggles its color between blue and red when clicked.
/// Uses the Input System (the project's active input handler), so clicks are detected by raycast
/// from the main camera rather than OnMouseDown, which needs the legacy Input Manager.
/// </summary>
[RequireComponent(typeof(Renderer), typeof(Collider))]
public class HelloSphere : MonoBehaviour
{
    [SerializeField] private Vector3 rotationSpeed = new Vector3(0f, 90f, 30f); // degrees per second
    [SerializeField] private Color colorA = Color.blue;
    [SerializeField] private Color colorB = Color.red;

    private Renderer sphereRenderer;
    private Collider sphereCollider;
    private bool isColorA = true;

    private void Awake()
    {
        sphereRenderer = GetComponent<Renderer>();
        sphereCollider = GetComponent<Collider>();
        SetColor(colorA);
    }

    private void Update()
    {
        transform.Rotate(rotationSpeed * Time.deltaTime, Space.Self);

        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            return;

        var cam = Camera.main;
        if (cam == null)
            return;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        if (sphereCollider.Raycast(ray, out _, 1000f))
            OnSphereClicked();
    }

    private void OnSphereClicked()
    {
        isColorA = !isColorA;
        SetColor(isColorA ? colorA : colorB);
        Debug.Log("Hello Unity from Claude!");
    }

    private void SetColor(Color color)
    {
        // .material creates a per-instance copy at runtime, leaving the shared asset untouched.
        sphereRenderer.material.color = color;
    }
}
