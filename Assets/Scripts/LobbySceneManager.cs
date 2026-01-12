using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbySceneManager : MonoBehaviour
{
    public Button button;
    public string sceneName;
    void Start()
    {
        button = GetComponent<Button>();
    }
    public void OnClickButton()
    {
        SceneManager.LoadScene(sceneName);
    }
}
