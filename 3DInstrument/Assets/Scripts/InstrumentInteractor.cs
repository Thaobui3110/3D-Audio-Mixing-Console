// Assets/Scripts/Player/InstrumentInteractor.cs
using UnityEngine;

public class InstrumentInteractor : MonoBehaviour {
    [Header("Interaction")]
    public float interactionRange = 3f;
    public LayerMask instrumentLayer;
    public KeyCode interactKey = KeyCode.E;
    
    [Header("Crosshair")]
    public UnityEngine.UI.Image crosshair;
    public Color normalColor = Color.white;
    public Color hoverColor = Color.yellow;
    
    private FPSController fpsController;

    void Start() {
        fpsController = GetComponentInParent<FPSController>();
    }

    void Update() {
        // Chỉ raycast khi đang ở FPS mode (cursor locked)
        if (fpsController != null && !fpsController.interactMode) {
            RaycastInteraction();
        }
        
        // Click interaction (dành cho interact mode)
        if (Input.GetMouseButtonDown(0)) {
            ClickInteraction();
        }
    }

    void RaycastInteraction() {
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit, interactionRange, instrumentLayer)) {
            // Hover effect
            if (crosshair != null) {
                crosshair.color = hoverColor;
            }
            
            // Press E to interact
            if (Input.GetKeyDown(interactKey)) {
                IInteractable interactable = hit.collider.GetComponent<IInteractable>();
                if (interactable != null) {
                    interactable.Interact(hit);
                }
            }
        } else {
            if (crosshair != null) {
                crosshair.color = normalColor;
            }
        }
    }

    void ClickInteraction() {
        // Click chuột để tương tác (khi cursor free)
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit, interactionRange, instrumentLayer)) {
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();
            if (interactable != null) {
                interactable.Interact(hit);
            }
        }
    }
}