using UnityEngine;

/// <summary>Starts the mission from the title overlay without changing scenes.</summary>
public sealed class MainMenuController : MonoBehaviour
{
    [SerializeField] private GameObject titleScreen;
    private bool hasStarted;

    private void Start()
    {
        hasStarted = false;
        Time.timeScale = 0f;
        if (titleScreen != null)
            titleScreen.SetActive(true);
    }

    public void StartGame()
    {
        if (hasStarted)
            return;

        hasStarted = true;
        Time.timeScale = 1f;
        if (titleScreen != null)
            titleScreen.SetActive(false);
    }

    private void OnDisable()
    {
        // Avoid leaving the rest of the Editor or a reloaded scene paused.
        if (!hasStarted && Time.timeScale == 0f)
            Time.timeScale = 1f;
    }
}
