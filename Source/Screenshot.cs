using System;
using UnityEngine;
using System.Reflection;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using AssetManagers;
using TMPro;
using System.Linq;

namespace Screenshot
{
    // main class called from Assembly-CSharp / GameLoader_CircleLoading
    public class ScreenshotRun
    {
        public ScreenshotRun()
        {
            // prevent duplicates
            if (ScreenshotRun.go == null)
            {
                ScreenshotRun.go = new GameObject();
                ScreenshotRun.grabber = go.AddComponent<ScreenGrabber>();
            }
        }

        static GameObject go;
        static ScreenGrabber grabber;
    }

    [Serializable]
    public class PluginConfigs
    {
        [NonSerialized]
        bool _config_fixed = false;
        [NonSerialized]
        string _config_path;

        // actual configs
        public bool enabled = true;
        public float pixels_per_tile = 10f;
        public float frequency_in_minutes = 2f;
        public bool only_current_floor = false;
        public List<int> selected_floors = new List<int>{0,1,-1};
        public bool debug_messages = false;
        public bool disable_zone_labels = true;
        public bool disable_daynightcycle = true;
        public bool disable_weather = true;
        
        public PluginConfigs(){
            this._config_path = Path.Combine(Application.persistentDataPath, "Screenshot-config.json");
        }

        // load data from json file (create if not exists)
        public void Load()
        {
            if (File.Exists(this._config_path))
            {
                var configText = File.ReadAllText(this._config_path.StripIncompatableQuotes());
                if (configText != null && configText != string.Empty && configText.Trim() != string.Empty)
                {
                    JsonUtility.FromJsonOverwrite(configText, this);
                    this.CheckConfig();
                }
                else
                {
                    this.Save();
                }
            }
            else
            {
                this.Save();
            }
        }

        // save data to json file (create if not exists)
        public void Save()
        {
            File.WriteAllText(this._config_path.StripIncompatableQuotes(), JsonUtility.ToJson(this, true));
        }
        // valid data and update file
        private void CheckConfig()
        {
            if (this.pixels_per_tile < 1f || this.pixels_per_tile > 25f) {
                this.pixels_per_tile = 10f;
                this._config_fixed = true;
            }

            if (this.frequency_in_minutes < 0.1f || this.frequency_in_minutes > 20f) {
                this.frequency_in_minutes = 2f;
                this._config_fixed = true;
            }

            var c = this.selected_floors.Count;
            this.selected_floors = this.selected_floors.Where(x => x >= -2 && x <= 2).ToList();
            this.selected_floors = this.selected_floors.Union(this.selected_floors).ToList();
            if(c != this.selected_floors.Count)
            {
                this._config_fixed = true;
            }
            if(this.selected_floors.Count <= 0)
            {
                this.selected_floors = new List<int>{0,1,-1};
            }

            // save data if changed
            if (this._config_fixed)
            {
                this.Save();
                this._config_fixed = false;
            }
        }
    }

    // Class attached to our GameObject
    public class ScreenGrabber : MonoBehaviour
    {
        private float nextScreenAt = 0f;
        private bool lockRun = false;

        private PluginConfigs config;

        public void Awake()
        {
            this.config = new PluginConfigs();
            this.config.Load();
        }

        public void Update()
        {
            if (!this.config.enabled)
                return;

            // check if we can tak another shot
            if (Time.realtimeSinceStartup > this.nextScreenAt)
            {
                this.nextScreenAt = Time.realtimeSinceStartup + (this.config.frequency_in_minutes * 60f);
                this.DoTheShot();
            }
        }

        private Coroutine DoTheShot()
        {
            if (this.lockRun || !Game.isLoaded)
                return null;

            this.lockRun = true;
            return Game.current.StartCoroutine(this.Screen_Procedure());
        }

        private Vector3 original_position;
        private float original_size;
        private int original_level;

        private float startTime = Time.realtimeSinceStartup;

        private void time(string info = "")
        {
            if (!this.config.debug_messages)
                return;

            Debug.LogWarning("Screenshot: Time Checkpoint ("+info+"):"+ (Time.realtimeSinceStartup - this.startTime));
            this.startTime = Time.realtimeSinceStartup;
        }

        public static bool nameContainsRestrictedChars(string name)
        {
            return (name.Contains("<") || name.Contains(">") || name.Contains(":") || name.Contains("\"") || name.Contains("/") || name.Contains("\\") || name.Contains("|") || name.Contains("?") || name.Contains("*"));
        }

        private IEnumerator Screen_Procedure()
        {
            this.time("Start");

            // Create Screenshot Directory if not exist
            string last_name = PlayerPrefs.GetString("recent_load_path", string.Empty);
            if (last_name != string.Empty)
            {
                last_name = Path.GetFileNameWithoutExtension(last_name);
            }
            if (last_name == string.Empty || ScreenGrabber.nameContainsRestrictedChars(last_name))
            {
                last_name = "screenshot";
            }
            
            string savePath = Path.Combine(Application.persistentDataPath, "Screen");
            if (!Directory.Exists(savePath))
                Directory.CreateDirectory(savePath);
            string saveName = string.Format("{0:yyyyMMddTHHmmssZ}-{1}", DateTime.Now, last_name);

            // Block screen and show message
            ConfirmMenu cm = ConfirmMenu.Show("Screenshot", "Taking screenshot", null, ConfirmMenu.MENU_OPT.EXCLUSIVE_KEYBINDINGS | ConfirmMenu.MENU_OPT.BLOCK_RAYCASTS | ConfirmMenu.MENU_OPT.NO_TRANSLATE | ConfirmMenu.MENU_OPT.PAUSE_GAME | ConfirmMenu.MENU_OPT.NO_FADE);
            var stuffToHide = new List<CanvasGroup>(1){
                cm.blockerMask
            };
            //yield return null;


            // save camera position and config
            this.original_position = Camera.main.transform.position;
            this.original_size = Camera.main.orthographicSize;
            this.original_level = UILevelSelector.CURRENT_FLOOR;

            // set camera to capture map
            Camera main = Camera.main;
            main.transform.position = new Vector3(Game.current.Map().size.x / 2f + (float)Game.current.Map().minX, Game.current.Map().size.y / 2f + (float)Game.current.Map().minY, main.transform.position.z);
            main.orthographicSize = Game.current.Map().size.y / 2f;

            // hide unwanted objects
            this.HideHUDExceptTop(false, stuffToHide);
            
            // calculate image pixel size
            int tw = Mathf.CeilToInt(Game.current.Map().size.x * this.config.pixels_per_tile);
            int th = Mathf.CeilToInt(Game.current.Map().size.y * this.config.pixels_per_tile);
            
            // RenderTexture settings
            RenderTexture rt = new RenderTexture(tw, th, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            main.targetTexture = rt;

            // Make textures for every floor
            Dictionary<int, Texture2D> textures = new Dictionary<int, Texture2D>();

            this.time("Initial config done");

            if (this.config.only_current_floor)
            {
                yield return null;
                int k = 100;
                main.Render();
                textures[k] = new Texture2D(tw, th, TextureFormat.RGB24, false);
                RenderTexture.active = rt;
                textures[k].ReadPixels(new Rect(0f, 0f, (float)tw, (float)th), 0, 0, false);
                this.time("Texture for current floor created");
            }
            else
            {
                foreach (var k in this.config.selected_floors)
                {
                    // move to level
                    UILevelSelector.instance.SetCurrentLevel(k);
                    yield return null;
                    main.Render();
                    textures[k] = new Texture2D(tw, th, TextureFormat.RGB24, false);
                    RenderTexture.active = rt;
                    textures[k].ReadPixels(new Rect(0f, 0f, (float)tw, (float)th), 0, 0, false);
                    this.time("Texture for floor "+k+" created");
                }
            }

            // revert any changes in config
            main.transform.position = this.original_position;
            main.orthographicSize = this.original_size;

            this.HideHUDExceptTop(true, stuffToHide);

            if (!this.config.only_current_floor)
            {
                UILevelSelector.instance.SetCurrentLevel(this.original_level);
            }

            RenderTexture.active = null;
            main.targetTexture = null;
            rt.Release();

            // remove message
            UnityEngine.Object.Destroy(cm.gameObject);

            yield return null;

            this.time("Starting to save data");
            if (this.config.only_current_floor)
            {
                int k = 100;
                ScreenGrabber.SaveFile job = new ScreenGrabber.SaveFile(textures[k], Path.Combine(savePath, "CF-" + saveName + ".png"));
                yield return job.Wait();
                this.time("File saved (current floor)");
            }
            else
            {
                foreach (var k in this.config.selected_floors)
                {
                    var floorName = (k < 0 ? "U" + Math.Abs(k) : "F" + k);
                    ScreenGrabber.SaveFile job = new ScreenGrabber.SaveFile(textures[k], Path.Combine(savePath, floorName + "-" + saveName + ".png"));
                    yield return job.Wait();
                    this.time("File saved (floor " + k+")");
                }
            }

            // show confirmation (money history floating text)
            if (this.textMeshProUGUI == null)
            {
                var gm = Game.current._money;
                this.textMeshProGoUGUI = UnityEngine.Object.Instantiate<GameObject>(gm.prefab_MoneyIndicatorLineItem);
                textMeshProGoUGUI.transform.SetParent(gm.txtMoneyIndicatorContainer.transform);
                this.textMeshProUGUI = textMeshProGoUGUI.GetComponent<TextMeshProUGUI>();
            }
            textMeshProUGUI.fontStyle = FontStyles.Bold;

            textMeshProUGUI.text = "Screenshot taken!";
			textMeshProUGUI.color = Color.cyan;
			textMeshProUGUI.GetComponent<CanvasRenderer>().SetAlpha(1f);

		    base.StartCoroutine(this.TextMoneyFade(textMeshProUGUI));

            // prevent shots being taken too often
            if (Time.realtimeSinceStartup > (this.nextScreenAt-10f))
            {
                this.nextScreenAt = Time.realtimeSinceStartup + (this.config.frequency_in_minutes * 60f);
            }

            // unblock next time checking
            this.lockRun = false;
    

            yield break;
            
        }

        private GameObject textMeshProGoUGUI;
        private TextMeshProUGUI textMeshProUGUI;

        // helper function for fading out floating text
        private IEnumerator TextMoneyFade(TextMeshProUGUI item)
        {
            item.transform.SetAsFirstSibling();
            item.gameObject.SetActive(true);
            item.CrossFadeAlpha(0f, 5f, true);
            yield return new WaitForSecondsRealtime(4f);
            item.gameObject.SetActive(false);
            yield break;
        }

        // helper function for asynchronous file saving
        private class SaveFile : ThreadedJob
        {
            private Texture2D texture;
            private string path;
            public SaveFile(Texture2D texture, string path)
            {
                this.texture = texture;
                this.path = path;
            }
            
            protected override void ThreadFunction()
            {
                try
                {
                    byte[] bytes = this.texture.EncodeToPNG();
                    UnityEngine.Object.Destroy(texture);
                    File.WriteAllBytes(this.path, bytes);
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception);
                }
            }
        }

        private bool original_zone_text = true;
        private float original_underground_overlay = 0.2f;
        private bool original_weather_state = true;

        // helper function to hide or show unwanted objects
        public void HideHUDExceptTop(bool falseToHide = true, List<CanvasGroup> alsoHide = null)
        {
            Canvas component = Game.current.ui.transform.parent.GetComponent<Canvas>();
            if (!falseToHide)
            { // hide
                component.renderMode = RenderMode.ScreenSpaceCamera;
                component.worldCamera = Camera.main;
                Game.current.ui.DisplayFuelHUD(true, false);
                if (this.config.disable_zone_labels)
                {
                    this.original_zone_text = Game.current.ui.ZoneTextContainer.gameObject.activeSelf;
                    Game.current.ui.ZoneTextContainer.gameObject.SetActive(false);
                }
                this.original_underground_overlay = GameTimer.UndegroundOverlay;
                GameTimer.UndegroundOverlay = 0.0001f;
                UIHudReportToggle.KillAll();
                if (this.config.disable_daynightcycle)
                {
                    GameTimer.DEBUG_DAYLIGHTONLY = true;
                }
                if (this.config.disable_weather && Game.current?.Weather_Precipitation?.rainJitter?.rainSprite != null)
                {
                    this.original_weather_state = Game.current.Weather_Precipitation.rainJitter.rainSprite.enabled;
                    Game.current.Weather_Precipitation.rainJitter.rainSprite.enabled = false;
                }
            }
            else
            { // show
                component.renderMode = RenderMode.ScreenSpaceOverlay;
                component.worldCamera = null;
                Game.current.ui.DisplayFuelHUD(PlayerPrefs.GetInt("ShowFuelHUD", 0) == 1, false);
                if (this.config.disable_zone_labels)
                {
                    Game.current.ui.ZoneTextContainer.gameObject.SetActive(this.original_zone_text);
                }
                GameTimer.UndegroundOverlay = this.original_underground_overlay;
                if (this.config.disable_daynightcycle)
                {
                    GameTimer.DEBUG_DAYLIGHTONLY = false;
                }
                if (this.config.disable_weather && Game.current?.Weather_Precipitation?.rainJitter?.rainSprite != null)
                {
                    Game.current.Weather_Precipitation.rainJitter.rainSprite.enabled = this.original_weather_state;
                }
            }
            GameObject.Find("Right HUD Panel").GetComponent<CanvasGroup>().alpha = ((!falseToHide) ? 0f : 1f);
            GameObject.Find("Bottom HUD Panel").GetComponent<CanvasGroup>().alpha = ((!falseToHide) ? 0f : 1f);
            if (alsoHide != null)
            {
                foreach (CanvasGroup canvasGroup in alsoHide)
                {
                    canvasGroup.alpha = ((!falseToHide) ? 0f : 1f);
                }
            }
            if (GameObject.Find("HowToHelp") != null)
            {
                GameObject.Find("HowToHelp").SetActive(false);
            }
        }
    }
}