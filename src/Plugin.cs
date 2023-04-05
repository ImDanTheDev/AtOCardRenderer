using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AtOCCardRenderer
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private Renderer _renderer;
        private bool _visible;

        private void Awake()
        {
            _renderer = gameObject.AddComponent<Renderer>();
            _renderer.enabled = _visible;
            _renderer.logger = Logger;

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote) && Input.GetKey(KeyCode.LeftControl))
            {
                var activeScene = SceneManager.GetActiveScene();
                if (activeScene.name != "MainMenu") return;
                _visible = !_visible;
                _renderer.enabled = _visible;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode arg1)
        {
            if (SceneManager.GetActiveScene().name != "MainMenu")
            {
                _renderer.enabled = false;
            }
        }
    }
}
