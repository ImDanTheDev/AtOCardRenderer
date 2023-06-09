using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using ImageMagick;
using TMPro;
using UnityEngine;

namespace AtOCCardRenderer
{
    public class Renderer : MonoBehaviour
    {
        private const string RENDER_FOLDER = "RenderResults";
        private const string RENDER_SUMMARY = "RenderSummary.csv";
        private const float RENDER_ORTHO_SIZE = 1.7f;
        private const string NOTIFICATION_SOUND = "ui_item_usedcharge";

        public BepInEx.Logging.ManualLogSource logger;

        private RenderConfig _config;
        private RenderTexture _renderTexture;
        private Texture2D _exportTexture;
        private GameObject _cardItemGO;
        private CardItem _cardItem;
        private CSVBuilder _csv;

        private GameObject _trashMainMenuManager;
        private bool _trashMainMenuManagerOriginalState;
        private GameObject _trashCanvas;
        private bool _trashCanvasOriginalState;
        private GameObject _trashGameModes;
        private bool _trashGameModesOriginalState;
        private GameObject _trashCredits;
        private bool _trashCreditsOriginalState;
        private GameObject _trashCamera;
        private bool _trashCameraOriginalState;
        private GameObject _trashBackground;
        private bool _trashBackgroundOriginalState;

        private IEnumerator _renderTask;
        private bool _rendering;
        private bool _showAdvanced;
        private int _cardCount;
        private float _originalOrthoSize;

        private int _cardsProcessed;
        private int _cardsToProcess;
        private int _imagesRendered;
        private int _imagesToRender;
        private CancellationTokenSource _cancellationTokenSource;

        private void OnEnable()
        {
            logger.LogInfo("Renderer shown");
            Directory.CreateDirectory(RENDER_FOLDER);
            _config = new RenderConfig();
            FindTrash();
            SetTrashVisibility(false);

            _cardCount = Globals.Instance.Cards.Count;

            SetupCanvas();
            SetupTextures();
            SetupCard();

            _csv = new CSVBuilder(RENDER_SUMMARY);
        }

        private void Render(string fileName = "Sample")
        {
            _imagesToRender++;
            Camera.main.targetTexture = _renderTexture;
            RenderTexture.active = _renderTexture;
            Camera.main.Render();
            _exportTexture.ReadPixels(new Rect(_config.srcX, _config.srcY, _config.srcWidth, _config.srcHeight), _config.dstX, _config.dstY);
            _exportTexture.Apply();
            Camera.main.targetTexture = null;

            byte[] rawData = _exportTexture.GetRawTextureData();

            // TODO: Dont crop _Full cards.
            if (_config.crop)
            {
                ThreadPool.QueueUserWorkItem(Crop, (fileName, rawData, _exportTexture.width, _exportTexture.height));
            }
            else
            {
                ThreadPool.QueueUserWorkItem(ThreadedSave, (fileName, rawData, _exportTexture.width, _exportTexture.height));
            }
        }

        private void Crop(object obj)
        {
            if (_cancellationTokenSource.Token.IsCancellationRequested) return;
            (string, byte[], int, int) state = ((string, byte[], int, int))obj;
            var pngData = ImageConversion.EncodeArrayToPNG(state.Item2, _exportTexture.graphicsFormat, (uint)state.Item3, (uint)state.Item4);
            using var img = new MagickImage(pngData, MagickFormat.Png);
            var originalBorderColor = img.BorderColor;
            img.BorderColor.SetFromBytes(1, 1, 1, 1);
            img.Trim(new Percentage(95));
            img.BorderColor = originalBorderColor;
            img.Trim();
            img.Write($"{RENDER_FOLDER}/{state.Item1}.png");
            _imagesRendered++;
        }

        private void ThreadedSave(object obj)
        {
            if (_cancellationTokenSource.Token.IsCancellationRequested) return;
            (string, byte[], int, int) state = ((string, byte[], int, int))obj;
            var pngData = ImageConversion.EncodeArrayToPNG(state.Item2, _exportTexture.graphicsFormat, (uint)state.Item3, (uint)state.Item4);
            File.WriteAllBytes($"{RENDER_FOLDER}/{state.Item1}.png", pngData);
            _imagesRendered++;
        }

        private IEnumerator RenderRange(int firstIndex, int lastIndex)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _rendering = true;
            yield return null;
            Dictionary<string, CardData> cards = Globals.Instance.Cards;

            var range = cards.Values.Skip(firstIndex).Take(lastIndex - firstIndex + 1);
            _cardsToProcess = lastIndex - firstIndex + 1;
            _cardsProcessed = 0;
            _imagesToRender = 0;
            _imagesRendered = 0;

            List<GameObject> toRender = new();
            List<GameObject> skipped = new();

            // TODO: Only need to do this once, not at the start of each ranged render.
            GameObject cardGO = _cardItem.transform.Find("CardGO").gameObject;
            GameObject lockGO = _cardItem.transform.Find("Lock").gameObject;
            List<(GameObject, SpriteRenderer, TMP_Text)> children = new();
            foreach (Transform child in cardGO.transform)
            {
                children.Add((child.gameObject, child.GetComponent<SpriteRenderer>(), child.GetComponent<TMP_Text>()));
            }

            foreach (CardData card in range)
            {
                _cardItem.SetCard(card.Id, false, null, null, false, false);
                Render(card.Id + "_Full");
                if (_config.onlyRenderFullCards)
                {
                    _csv.AddCard(card, toRender);
                    yield return null;
                    continue;
                }

                if (lockGO.activeSelf)
                {
                    toRender.Add(lockGO);
                    lockGO.SetActive(false);
                }

                foreach ((GameObject go, SpriteRenderer sr, TMP_Text txt) in children)
                {
                    if (!go.activeSelf) continue;
                    if (sr?.sprite == null || txt?.text.Length == 0)
                    {
                        skipped.Add(go);
                        go.SetActive(false);
                        continue;
                    }
                    toRender.Add(go);
                    go.SetActive(false);
                }

                for (int i = toRender.Count - 1; i >= 0; i--)
                {
                    var elem = toRender[i];
                    elem.SetActive(true);
                    if (i + 1 < toRender.Count) toRender[i + 1].SetActive(false);
                    Render($"{card.Id}_{elem.name}");
                }

                toRender.ForEach(x => x.SetActive(true));
                skipped.ForEach(x => x.SetActive(true));

                toRender.Clear();
                skipped.Clear();

                _csv.AddCard(card, toRender);
                _cardsProcessed++;
                yield return null;
            }

            _csv.Write();
            _csv.Reset();

            yield return new WaitUntil(() => _imagesRendered == _imagesToRender);
            _cancellationTokenSource.Cancel();
            _renderTask = null;
            _imagesToRender = 0;
            _imagesRendered = 0;
            _cardsProcessed = 0;
            _cardsToProcess = 0;
            _rendering = false;
            GameManager.Instance.PlayLibraryAudio(NOTIFICATION_SOUND, 0f);

            logger.LogInfo($"Finished rendering range.");
        }

        private void OnGUI()
        {
            GUI.enabled = !_rendering;
            _showAdvanced = GUILayout.Toggle(_showAdvanced, "Show Advanced Options");
            if (_showAdvanced)
            {
                GUILayout.Label("RT Width");
                if (int.TryParse(GUILayout.TextField(_config.renderTextureWidth.ToString()), out int rtw)) _config.renderTextureWidth = rtw;
                GUILayout.Label("RT Height");
                if (int.TryParse(GUILayout.TextField(_config.renderTextureHeight.ToString()), out int rth)) _config.renderTextureHeight = rth;
                GUILayout.Label("ET Width");
                if (int.TryParse(GUILayout.TextField(_config.exportTextureWidth.ToString()), out int etw)) _config.exportTextureWidth = etw;
                GUILayout.Label("ET Height");
                if (int.TryParse(GUILayout.TextField(_config.exportTextureHeight.ToString()), out int eth)) _config.exportTextureHeight = eth;

                GUILayout.Label("Src X");
                if (float.TryParse(GUILayout.TextField(_config.srcX.ToString()), out float srcx)) _config.srcX = srcx;
                GUILayout.Label("Src Y");
                if (float.TryParse(GUILayout.TextField(_config.srcY.ToString()), out float srcy)) _config.srcY = srcy;
                GUILayout.Label("Src Width");
                if (float.TryParse(GUILayout.TextField(_config.srcWidth.ToString()), out float srcw)) _config.srcWidth = srcw;
                GUILayout.Label("Src Height");
                if (float.TryParse(GUILayout.TextField(_config.srcHeight.ToString()), out float srch)) _config.srcHeight = srch;

                GUILayout.Label("Dst X");
                if (int.TryParse(GUILayout.TextField(_config.dstX.ToString()), out int dstx)) _config.dstX = dstx;
                GUILayout.Label("Dst Y");
                if (int.TryParse(GUILayout.TextField(_config.dstY.ToString()), out int dsty)) _config.dstY = dsty;

                if (GUILayout.Button("Apply Advanced Settings"))
                {
                    DestroyImmediate(_renderTexture);
                    DestroyImmediate(_exportTexture);

                    SetupTextures();
                }
                if (GUILayout.Button("Render Sample")) Render();
            }

            _config.crop = GUILayout.Toggle(_config.crop, "Crop");

            GUILayout.Label($"Number of Cards: {_cardCount}");
            GUILayout.Label("Range Start Index");
            if (int.TryParse(GUILayout.TextField(_config.rangeStart.ToString()), out int rsi))
            {
                _config.rangeStart = Mathf.Clamp(rsi, 0, _cardCount - 1);
            }

            GUILayout.Label("Range End Index");
            if (int.TryParse(GUILayout.TextField(_config.rangeEnd.ToString()), out int rei))
            {
                _config.rangeEnd = Mathf.Clamp(rei, 0, _cardCount - 1);
            }

            _config.onlyRenderFullCards = GUILayout.Toggle(_config.onlyRenderFullCards, "Only render assembled cards");

            if (GUILayout.Button("Render Range"))
            {
                int start = Mathf.Min(_config.rangeStart, _config.rangeEnd);
                int end = Mathf.Max(_config.rangeStart, _config.rangeEnd);
                _renderTask = RenderRange(start, end);
                StartCoroutine(_renderTask);
            }

            GUI.enabled = true;
            if (_renderTask != null)
            {
                GUILayout.Label($"Cards Processed: {_cardsProcessed}/{_cardsToProcess}");
                GUILayout.Label($"Render Progress: {_imagesRendered}/{_imagesToRender}");
                if (GUILayout.Button("Stop Render Range"))
                {
                    StopCoroutine(_renderTask);
                    _renderTask = null;
                    _imagesToRender = 0;
                    _imagesRendered = 0;
                    _cardsProcessed = 0;
                    _cardsToProcess = 0;
                    _rendering = false;
                    _cancellationTokenSource.Cancel();
                }
            }
        }

        private void SetupCanvas()
        {
            _originalOrthoSize = Camera.main.orthographicSize;
            Camera.main.orthographicSize = RENDER_ORTHO_SIZE;
        }

        private void FindTrash()
        {
            if (!_trashMainMenuManager) _trashMainMenuManager = GameObject.Find("/MainMenuManager");
            if (!_trashCanvas) _trashCanvas = GameObject.Find("/Canvas");
            if (!_trashBackground) _trashBackground = GameObject.Find("/Background");
            if (!_trashCamera) _trashCamera = GameObject.Find("/Camera");
            if (!_trashGameModes) _trashGameModes = GameObject.Find("/GameModes");
            if (!_trashCredits) _trashCredits = GameObject.Find("/Credits");

            _trashMainMenuManagerOriginalState = _trashMainMenuManager?.activeSelf ?? true;
            _trashCanvasOriginalState = _trashCanvas?.activeSelf ?? true;
            _trashBackgroundOriginalState = _trashBackground?.activeSelf ?? true;
            _trashCameraOriginalState = _trashCamera?.activeSelf ?? true;
            _trashGameModesOriginalState = _trashGameModes?.activeSelf ?? true;
            _trashCreditsOriginalState = _trashCredits?.activeSelf ?? true;
        }

        private void SetTrashVisibility(bool visible)
        {
            _trashMainMenuManager?.SetActive(visible);
            _trashBackground?.SetActive(visible);
            _trashCanvas?.SetActive(visible);

            _trashCamera?.SetActive(visible);
            _trashGameModes?.SetActive(visible);
            _trashCredits?.SetActive(visible);
        }

        private void ResetTrashVisibility()
        {
            _trashMainMenuManager?.SetActive(_trashMainMenuManagerOriginalState);
            _trashCanvas?.SetActive(_trashCanvasOriginalState);
            _trashBackground?.SetActive(_trashBackgroundOriginalState);

            _trashCamera?.SetActive(_trashCameraOriginalState);
            _trashGameModes?.SetActive(_trashGameModesOriginalState);
            _trashCredits?.SetActive(_trashCreditsOriginalState);
        }

        private void SetupTextures()
        {
            DestroyTextures();

            _renderTexture = new RenderTexture(_config.renderTextureWidth, _config.renderTextureHeight, 32, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            _exportTexture = new Texture2D(_config.exportTextureWidth, _config.exportTextureHeight, TextureFormat.ARGB32, false);
        }

        private void SetupCard()
        {
            _cardItemGO = Instantiate(GameManager.Instance.CardPrefab);
            _cardItemGO.name = "Render Card";

            _cardItem = _cardItemGO.GetComponent<CardItem>();
        }

        private void DestroyTextures()
        {
            if (_renderTexture) DestroyImmediate(_renderTexture);
            if (_exportTexture) DestroyImmediate(_exportTexture);
        }

        private void OnDisable()
        {
            ResetTrashVisibility();
            DestroyTextures();
            DestroyImmediate(_cardItemGO);
            Camera.main.orthographicSize = _originalOrthoSize;

            logger.LogInfo("Renderer hidden");
        }

        private struct RenderConfig
        {
            public int renderTextureWidth = 1920;
            public int renderTextureHeight = 1080;
            public int exportTextureWidth = 542;
            public int exportTextureHeight = 814;

            public float srcX = 690f;
            public float srcY = 133f;
            public float srcWidth = 542f;
            public float srcHeight = 814f;

            public int dstX = 0;
            public int dstY = 0;

            public int rangeStart = 0;
            public int rangeEnd = 4;
            public bool crop = false;

            public bool onlyRenderFullCards;

            public RenderConfig() { }
        }
    }

}