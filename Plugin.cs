using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AtOCCardRenderer
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private Canvas _renderCanvas;
        private RenderTexture _renderTexture;
        private Texture2D _exportTexture;
        private CardItem _cardItem;

        private int _renderTextureWidth = 800;
        private int _renderTextureHeight = 768;
        private int _exportTextureWidth = 400;
        private int _exportTextureHeight = 600;

        private float _srcX = 200f;
        private float _srcY = 85f;
        private float _srcWidth = 600f;
        private float _srcHeight = 768f;

        private int _dstX = 0;
        private int _dstY = 0;

        private int _rangeStart = 0;
        private int _rangeEnd = 5;

        private IEnumerator _renderTask;
        private string _csvHeader;
        private string _csvText;
        private PropertyInfo[] _cardDataProperties;

        private int _cardCount;

        private bool _showAdvanced;
        private int _cardsRendered;
        private int _cardsToRender;
        private bool _rendering;

        private void Awake()
        {
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnGUI()
        {
            GUI.enabled = !_rendering;
            _showAdvanced = GUILayout.Toggle(_showAdvanced, "Show Advanced Options");
            if (_showAdvanced)
            {
                GUILayout.Label("RT Width");
                if (int.TryParse(GUILayout.TextField(_renderTextureWidth.ToString()), out int rtw)) _renderTextureWidth = rtw;
                GUILayout.Label("RT Height");
                if (int.TryParse(GUILayout.TextField(_renderTextureHeight.ToString()), out int rth)) _renderTextureHeight = rth;
                GUILayout.Label("ET Width");
                if (int.TryParse(GUILayout.TextField(_exportTextureWidth.ToString()), out int etw)) _exportTextureWidth = etw;
                GUILayout.Label("ET Height");
                if (int.TryParse(GUILayout.TextField(_exportTextureHeight.ToString()), out int eth)) _exportTextureHeight = eth;

                GUILayout.Label("Src X");
                if (float.TryParse(GUILayout.TextField(_srcX.ToString()), out float srcx)) _srcX = srcx;
                GUILayout.Label("Src Y");
                if (float.TryParse(GUILayout.TextField(_srcY.ToString()), out float srcy)) _srcY = srcy;
                GUILayout.Label("Src Width");
                if (float.TryParse(GUILayout.TextField(_srcWidth.ToString()), out float srcw)) _srcWidth = srcw;
                GUILayout.Label("Src Height");
                if (float.TryParse(GUILayout.TextField(_srcHeight.ToString()), out float srch)) _srcHeight = srch;

                GUILayout.Label("Dst X");
                if (int.TryParse(GUILayout.TextField(_dstX.ToString()), out int dstx)) _dstX = dstx;
                GUILayout.Label("Dst Y");
                if (int.TryParse(GUILayout.TextField(_dstY.ToString()), out int dsty)) _dstY = dsty;

                if (GUILayout.Button("Apply Advanced Settings"))
                {
                    DestroyImmediate(_renderTexture);
                    DestroyImmediate(_exportTexture);

                    SetupTextures();
                }
                if (GUILayout.Button("Render Sample")) Render();
            }

            GUILayout.Label($"Number of Cards: {_cardCount}");
            GUILayout.Label("Range Start Index");
            if (int.TryParse(GUILayout.TextField(_rangeStart.ToString()), out int rsi))
            {
                _rangeStart = Mathf.Clamp(rsi, 0, _cardCount - 1);
            }

            GUILayout.Label("Range End Index");
            if (int.TryParse(GUILayout.TextField(_rangeEnd.ToString()), out int rei))
            {
                _rangeEnd = Mathf.Clamp(rei, 0, _cardCount - 1);
            }

            if (GUILayout.Button("Render Range"))
            {
                int start = Mathf.Min(_rangeStart, _rangeEnd);
                int end = Mathf.Max(_rangeStart, _rangeEnd);
                _renderTask = RenderRange(start, end);
                StartCoroutine(_renderTask);
            }

            GUI.enabled = true;
            if (_renderTask != null)
            {
                GUILayout.Label($"Progress: {_cardsRendered}/{_cardsToRender}");
                if (GUILayout.Button("Stop Render Range"))
                {
                    StopCoroutine(_renderTask);
                    _renderTask = null;
                    _cardsRendered = 0;
                    _cardsToRender = 0;
                    _rendering = false;
                }
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode arg1)
        {
            if (scene.name == "MainMenu")
            {
                Directory.CreateDirectory("RenderResults");

                _cardDataProperties = typeof(CardData).GetProperties();
                _csvHeader = string.Join(",", _cardDataProperties.Select(x => $"\"{x.Name}\"")) + ",sections";
                _csvText = "";
                _cardCount = Globals.Instance.Cards.Count;

                SetupCanvas();
                AnnihilateTrash();

                SetupTextures();

                SetupCard();
            }
        }

        private void Render(string name = "Sample")
        {
            Camera.main.targetTexture = _renderTexture;
            RenderTexture.active = _renderTexture;
            Camera.main.Render();
            _exportTexture.ReadPixels(new Rect(_srcX, _srcY, _srcWidth, _srcHeight), _dstX, _dstY);
            _exportTexture.Apply();

            File.WriteAllBytes($"RenderResults/{name}.png", _exportTexture.EncodeToPNG());
            Camera.main.targetTexture = null;
        }

        private IEnumerator RenderRange(int firstIndex, int lastIndex)
        {
            _rendering = true;
            yield return null;
            Dictionary<string, CardData> cards = Globals.Instance.Cards;

            var range = cards.Values.Skip(firstIndex).Take(lastIndex - firstIndex + 1);
            _cardsToRender = lastIndex - firstIndex + 1;
            _cardsRendered = 0;

            List<GameObject> toRender = new();
            List<GameObject> skipped = new();

            _csvText = _csvHeader;
            foreach (CardData card in range)
            {
                string elements = "";
                _cardItem.SetCard(card.Id, true, null, null, false, false);
                Render(card.Id + "_Full");
                GameObject cardGO = _cardItem.transform.Find("CardGO").gameObject;

                var lockGO = _cardItem.transform.Find("Lock").gameObject;
                if (lockGO.activeSelf)
                {
                    toRender.Add(lockGO);
                    lockGO.SetActive(false);
                }

                foreach (Transform child in cardGO.transform)
                {
                    GameObject go = child.gameObject;
                    if (go.activeSelf)
                    {
                        if (go.GetComponent<SpriteRenderer>()?.sprite == null || go.GetComponent<TMP_Text>()?.text.Length == 0)
                        {
                            skipped.Add(go);
                            go.SetActive(false);
                            continue;
                        }
                        toRender.Add(go);
                        go.SetActive(false);
                    }
                }

                for (int i = toRender.Count - 1; i >= 0; i--)
                {
                    toRender[i].SetActive(true);
                    if (i + 1 < toRender.Count) toRender[i + 1].SetActive(false);
                    elements += toRender[i].name;
                    if (i != 0) elements += "|";
                    Render(card.Id + "_" + toRender[i].name);
                }
                toRender.ForEach(x => x.SetActive(true));
                skipped.ForEach(x => x.SetActive(true));

                toRender.Clear();

                // Append CSV
                _csvText += Environment.NewLine;
                var propertyValues = _cardDataProperties.Select((x, j) =>
                {
                    var value = x.GetValue(card);
                    return $"\"{x.GetValue(card)?.ToString()}\"";
                });
                _csvText += string.Join(",", propertyValues).Replace("\n", "");
                _csvText += $",{elements}";

                _cardsRendered++;
                yield return null;
            }

            File.WriteAllText("RenderSummary.csv", _csvText);
            GameManager.Instance.PlayLibraryAudio("ui_item_usedcharge", 0f);

            _renderTask = null;
            _cardsRendered = 0;
            _cardsToRender = 0;
            _rendering = false;

            Logger.LogInfo($"Finished rendering range.");
        }

        private void SetupTextures()
        {
            if (_renderTexture) DestroyImmediate(_renderTexture);
            if (_exportTexture) DestroyImmediate(_exportTexture);

            _renderTexture = new RenderTexture(_renderTextureWidth, _renderTextureHeight, 32, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            _exportTexture = new Texture2D(_exportTextureWidth, _exportTextureHeight, TextureFormat.ARGB32, false);
        }

        private void SetupCanvas()
        {
            Camera.main.orthographicSize = 1.7f;
            GameObject canvasGO = new("Render Canvas");
            _renderCanvas = canvasGO.AddComponent<Canvas>();
            _renderCanvas.renderMode = RenderMode.WorldSpace;
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.scaleFactor = 0.7f;
        }

        private void AnnihilateTrash()
        {
            GameObject.Find("/MainMenuManager")?.SetActive(false);
            GameObject.Find("/Camera")?.SetActive(false);
            GameObject.Find("/Canvas")?.SetActive(false);
            GameObject.Find("/Background")?.SetActive(false);
        }

        private void SetupCard()
        {
            GameObject cardItemGO = Instantiate(GameManager.Instance.CardPrefab);
            cardItemGO.name = "Render Card";
            cardItemGO.transform.parent = _renderCanvas.transform;

            _cardItem = cardItemGO.GetComponent<CardItem>();
        }

        private void OnDestroy()
        {
            DestroyImmediate(_renderTexture);
            DestroyImmediate(_exportTexture);
        }
    }
}
