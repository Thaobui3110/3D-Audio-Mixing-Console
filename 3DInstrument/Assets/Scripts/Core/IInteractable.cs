using UnityEngine;

public interface IInteractable
{
    void Interact(RaycastHit hit);
    string GetInteractPrompt(); // Hiện text HUD khi nhìn vào
}
