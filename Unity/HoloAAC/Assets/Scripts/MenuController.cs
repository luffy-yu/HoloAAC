using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using Microsoft.MixedReality.Toolkit.UI;
using TMPro;
using UnityEngine;

// GridGameObject types
public enum GridGameObjectType {
    KEYWORD,
    OBJECT,
    SENTENCE,
    OPTIONS, // all object options
};

public class MenuController : MonoBehaviour
{
    [Tooltip("Keywords GridObject Collection")]
    [SerializeField] private GameObject keywordGridGameObject;

    [Tooltip("Objects GridObject Collection")]
    [SerializeField] private GameObject objectGridGameObject;

    [Tooltip("Sentences GridObject Collection")]
    [SerializeField] private GameObject sentenceGridGameObject;

    [Tooltip("All Objects GridObject Collection")]
    [SerializeField] private GameObject allObjectsGridGameObject;

    [Tooltip("Music Loader for sentence")]
    [SerializeField] private MusicLoader musicLoader;

    // color controller for keywords 
    [Tooltip("Text Controller for keywords")]
    [SerializeField] private TextController textController;

    // voice switcher
    [Tooltip("Voice switch in hand menu")]
    [SerializeField] private GameObject voiceSwitcher;

    [Tooltip("Voice rate slider in hand menu")]
    [SerializeField] private GameObject voiceRateSlider;

    [Tooltip("Voice volume slider in hand menu")]
    [SerializeField] private GameObject voiceVolumeSlider;

    // get sentences when pressing button
    // get via SettingsButtonPressEvent.GetServiceHost()
    [Tooltip("Web API Host")]
    private string host = "";

    [Tooltip("Web API makesentences URL")]
    [SerializeField] private string url = "";

    [Tooltip("Web API updatefrequency URL")]
    [SerializeField] private string ufUrl = "";

    // timer for web request
    [Tooltip("Show timer")]
    [SerializeField] private bool showTimer = false;

    [Tooltip("Timer object")]
    [SerializeField] private TMP_Text timerText;

    // dialog controller to show alert information
    [Tooltip("Dialog Controller")]
    [SerializeField] private DialogController dialogController;

    [Tooltip("ButtonPressEvent Reference")]
    [SerializeField] private ButtonPressEvent buttonPressEvent;

    [Tooltip("Root key from web response")]
    private string rootKey;

    [Tooltip("Objects from web response")]
    private string[] objects;

    [Tooltip("Keywords from web response")]
    private string[] keywords;

    [Tooltip("Sentences from web response")]
    private string[] sentences;

    //// All possible objects in detected object
    //private string[] allObjects = new string[] {"beans","cake","candy","cereal","chips",
    //        "chocolate","coffee","corn","fish","flour",
    //        "honey","jam","juice","milk","nuts",
    //        "oil","pasta","rice","soda","spices",
    //        "sugar","tea","tomato sauce","vinegar","water" }; //tomato_sauce to tomato sauce

    //// All possible objects in detected object
    //private string[] allObjects = new string[] {"beans","cake","candy","cereal",
    //        "chocolate","coffee","fish",
    //        "jam","juice","milk",
    //        "rice","soda","spices",
    //        "tomato sauce","water", }; //tomato_sauce to tomato sauce

    private string[] allObjects = new string[] {
        "candy","cereal","chips","chocolate","coffee",
        "corn","fish","flour","jam","milk",
        "pasta","soda","spices","tea","water"
    };

    // Ogg filenames in current page. It'll be not null when requesting in this page, in other words, changing object manually or selecting keywords
    private string[] oggFilenames = null;

    // timestamp for counting requests used time
    private long startTimestampMS = 0;

    // flag to indicate whether to overwrite objects
    private bool overwriteObjects = false;

    // Start is called before the first frame update
    void Start()
    {
        // init host
        host = SettingsButtonPressEvent.GetServiceHost();
#if UNITY_EDITOR
        host = "http://127.0.0.1:8000";
#endif
        // set whether to show timer
        if (timerText != null)
        {
            timerText.enabled = showTimer;
        }

        InitializeGridObjects(keywordGridGameObject, GridGameObjectType.KEYWORD, new string[] { }, false);
        InitializeGridObjects(objectGridGameObject, GridGameObjectType.OBJECT, new string[] { }, true);
        InitializeGridObjects(sentenceGridGameObject, GridGameObjectType.SENTENCE, new string[] { }, false);
        // All supported objects in object detection
        InitializeGridObjects(allObjectsGridGameObject, GridGameObjectType.OPTIONS, allObjects, true);
    }

    // called by self and ButtonPressEvent
    public void SetTimerText(long usedTime)
    {
        // write to log
        buttonPressEvent.WriteRunLog(LogType.TIME_COST, "" + usedTime + "ms");
        if (timerText != null)
        {
            timerText.text = "" + usedTime + " ms";
        }
    }

    public TMP_Text GetTimer()
    {
        return timerText;
    }

    public DialogController GetDialogController()
    {
        return dialogController;
    }

    // this is called in ButtonPressEvent
    public void UpdateVariables(string rk, string []obs, string []kws, string []sts, string []oggfs, bool owr)
    {
        // check objs whether is null, e.g., [""]
        if (obs != null && obs.Length == 1 && obs[0] == "")
            obs = null;

        bool needResetKeywords = true;

        // if overwriteObjects == false and keywords don't change
        if (this.keywords != null && kws != null && Enumerable.SequenceEqual(kws, this.keywords))
        {
            // update Keywords Color, i.e., set selected color
            needResetKeywords = false;
        }

        this.rootKey = rk;
        this.objects = obs;
        this.keywords = kws;
        this.sentences = sts;
        this.overwriteObjects = owr;

        this.oggFilenames = oggfs;

        // update UI
        UpdateUI(true, needResetKeywords, true);
        // change color when overwriting object
        if(this.overwriteObjects)
        {
            // initialize object color
            InitializeObjectWindowColor();
        }
    }

    // Update object buttons' color when first getting the response from server
    void InitializeObjectWindowColor()
    {
        // select all objects
        UpdateGGOTextColor(objectGridGameObject, false);
    }

    // update UI
    void UpdateUI(bool obj=true, bool kws = true, bool sts = true)
    {
        // update when overwriteObjects is true
        if (obj && this.overwriteObjects)
            InitializeGridObjects(objectGridGameObject, GridGameObjectType.OBJECT, this.objects, true);

        if (kws)
            InitializeGridObjects(keywordGridGameObject, GridGameObjectType.KEYWORD, this.keywords, false);

        if (sts)
            InitializeGridObjects(sentenceGridGameObject, GridGameObjectType.SENTENCE, this.sentences, false);
    }

    public void ResetOptionsPanel()
    {
        InitializeGridObjects(allObjectsGridGameObject, GridGameObjectType.OPTIONS, this.allObjects, true);
    }

    // reset when back to previous UI
    public void ResetUI()
    {
        objects = null;
        keywords = null;
        sentences = null;

        InitializeGridObjects(keywordGridGameObject, GridGameObjectType.KEYWORD, new string[] { }, false);
        InitializeGridObjects(objectGridGameObject, GridGameObjectType.OBJECT, new string[] { }, true);
        InitializeGridObjects(sentenceGridGameObject, GridGameObjectType.SENTENCE, new string[] { }, false);
        InitializeGridObjects(allObjectsGridGameObject, GridGameObjectType.OPTIONS, allObjects, true);

        oggFilenames = null;
        rootKey = null;
    }

    // get full filename in file system
    string GetFullPath(string filename)
    {
#if UNITY_EDITOR
        return @"D:\" + filename;
#endif
        return System.IO.Path.Combine(Application.persistentDataPath, filename);
    }

    // Play sound for button
    void PlayComponentAudioSource(Component component, string path, int index)
    {
        AudioSource audioSource = null;
        component.TryGetComponent<AudioSource>(out audioSource);
        if (audioSource == null) return;
        // set audio mixer group if needed
        if(audioSource.outputAudioMixerGroup == null)
        {
            audioSource.outputAudioMixerGroup = musicLoader.GetAudioMixerGroup();
        }
        StartCoroutine(musicLoader.LoadMusic(audioSource, path, CallbackAfterPlayingSound, index));
    }

    // Callback for playing sound
    void CallbackAfterPlayingSound(int index)
    {
        // set deselected color
        UpdateNthGGOTextColor(sentenceGridGameObject, index, false);
    }

    // Play n-th component sound
    void PlayNthComponentAudioSource(GameObject gameObject, int nth, string path)
    {
        if (gameObject == null) return;
        
        Component[] children = gameObject.GetComponentsInChildren<PressableButtonHoloLens2>(true);
        if (nth >= children.Length) return;

        PlayComponentAudioSource(children[nth], path, nth);
    }

    void InitializeGridObjects(GameObject gameObject, GridGameObjectType gridGameObjectType, string[] choices, bool setIconSet)
    {
        if (gameObject == null) return;
        // if choices is null, disable the gameObject
        if (choices == null)
        {
            gameObject.SetActive(false);
            return;
        } else
        {
            gameObject.SetActive(true);
        }
        Component[] children = gameObject.GetComponentsInChildren<PressableButtonHoloLens2>(true);
        setComponentsText(children, choices, true, setIconSet);
            
        // set all color to deselected color
        UpdateGGOTextColor(gameObject, true);
        
        // set press event
        SetComponentsPressEvent(children, gridGameObjectType);
    }

    // get ButtonConfigHelper of n-th node in GirdObjectCollection
    ButtonConfigHelper GetButtonConfigHelper(Component[] components, int nth)
    {
        if (nth >= components.Length) return null;
        ButtonConfigHelper bch = null;
        components[nth].TryGetComponent<ButtonConfigHelper>(out bch);
        return bch;
    }

    // set text of n-th node in GirdObjectCollection
    // for object, set icon set
    void SetNthComponentText(Component[] components, int nth, string text, bool setIconSet)
    {
        ButtonConfigHelper bch = GetButtonConfigHelper(components, nth);
        if (bch == null) return;
        bch.MainLabelText = text;
        if (setIconSet)
        {
            bch.SetQuadIconByName(text);
        }
    }

    // set activablity of n-th node in GridObjectCollection
    void SetNthComponentActivablity(Component[] components, int nth, bool active)
    {
        if (nth >= components.Length) return;
        components[nth].gameObject.SetActive(active);
    }

    // set text with a list of text
    void setComponentsText(Component[] components, string[] texts, bool hideLeftComponents, bool setIconSet)
    {
        int size = components.Length > texts.Length ? texts.Length : components.Length;
        for (int i = 0; i < size; i++)
        {
            SetNthComponentActivablity(components, i, true);
            SetNthComponentText(components, i, texts[i], setIconSet);
        }
        
        if(hideLeftComponents)
        {
            for(int i = size; i < components.Length; i++)
            {
                SetNthComponentActivablity(components, i, false);
            }
        }
    }

    // set press event for n-th node in GridObjectCollection
    void SetNthComponentPressEvent(Component[] components, int nth, GridGameObjectType gridGameObjectType)
    {
        ButtonConfigHelper bch = GetButtonConfigHelper(components, nth);
        if (bch == null) return;
        // ignore unactive objects
        if (components[nth].gameObject.activeSelf == false) return;
        bch.OnClick.RemoveAllListeners();
        // use `delegate` to receive parameters 
        bch.OnClick.AddListener(delegate { PressableButtonEvent(nth, gridGameObjectType); });
    }

    // set press event for all nodes in GridObjectCollection
    void SetComponentsPressEvent(Component[] components, GridGameObjectType gridGameObjectType)
    {
        for(int i = 0; i < components.Length; i++)
        {
            SetNthComponentPressEvent(components, i, gridGameObjectType);
        }
    }

    string GetOggfilename(int nth_sentence)
    {
        return oggFilenames[nth_sentence];
    }

    // write log
    void WriteLog(int index, GridGameObjectType gridGameObjectType)
    {
        if (this.buttonPressEvent == null)
        {
            return;
        }
        Component[] components = null;
        LogType lt;
        if(gridGameObjectType == GridGameObjectType.KEYWORD)
        {
            components = keywordGridGameObject.GetComponentsInChildren<PressableButtonHoloLens2>(false);
            lt = LogType.CLICK_KYW;
        }
        else if(gridGameObjectType == GridGameObjectType.OBJECT)
        {
            components = objectGridGameObject.GetComponentsInChildren<PressableButtonHoloLens2>(false);
            lt = LogType.CLICK_OBJ;
        }
        else if(gridGameObjectType == GridGameObjectType.SENTENCE)
        {
            components = sentenceGridGameObject.GetComponentsInChildren<PressableButtonHoloLens2>(false);
            lt = LogType.CLICK_STS;
        }
        else // options
        {
            components = allObjectsGridGameObject.GetComponentsInChildren<PressableButtonHoloLens2>(false);
            lt = LogType.CLICK_OPT;
        }
        if(components == null)
        {
            // Debug.LogError("Null found in WriteLog()");
            return;
        }
        TMP_Text text = GetNthTextMeshProText(components, index);
        // write
        this.buttonPressEvent.WriteRunLog(lt, text.text);
    }

    // press event for PressableButtonHoloLens2
    void PressableButtonEvent(int index, GridGameObjectType gridGameObjectType)
    {
        // Debug.LogError("Type:" + gridGameObjectType.ToString() + " " + index + " was pressed");
        // write log
        WriteLog(index, gridGameObjectType);
        // should not overwrite objects
        overwriteObjects = false;

        if (gridGameObjectType == GridGameObjectType.SENTENCE)
        {
            // set audio
            //PlayNthComponentAudioSource(sentenceGridGameObject, index, audioFile);
            if (oggFilenames != null)
            {
                string oggFilename = GetFullPath(GetOggfilename(index));
                // Debug.LogError("Ogg filename:" + oggFilename);
                PlayNthComponentAudioSource(sentenceGridGameObject, index, oggFilename);
                // update color
                UpdateNthGGOTextColor(sentenceGridGameObject, index, true);
                // request updating frequency
                RequestUpdateFrequency(index);
            }
            return;
        }
        else if(gridGameObjectType == GridGameObjectType.OBJECT)
        {
            // update color
            UpdateNthObjectTextColor(index);
            // clear keywords selection
            UpdateGGOTextColor(keywordGridGameObject, true);
        }
        else if(gridGameObjectType == GridGameObjectType.KEYWORD)
        {
            UpdateNthKeywordTextColor(index);
        } 
        else // Options
        {
            // update color
            UpdateNthOptionTextColor(index);
            // clear keywords selection
            UpdateGGOTextColor(keywordGridGameObject, true);
        }
        // clear oggFilenames immediately
        oggFilenames = null;
        // request sentences again
        RequestSentences();
    }

    // Get TextMesh Pro of n-th component
    TMP_Text GetNthTextMeshProText(Component[] components, int nth)
    {
        TMP_Text tMP_Text = null;

        if (nth >= components.Length) return tMP_Text;

        tMP_Text = components[nth].GetComponentInChildren<TMP_Text>(true);
        
        return tMP_Text;
    }

    // Get index of item having text
    int GetIndexOfText(Component[] components, string text)
    {
        int size = components.Length;
        for(int i = 0; i < size; i ++)
        {
            if (components[i].GetComponentInChildren<TMP_Text>(true).text == text)
                return i;
        }
        return -1;
    }

    // Update text color, initialization means first run
    void UpdateComponentsTextColor(Component[] components, bool initialization)
    {
        if (textController == null) return;
        for(int i = 0;i < components.Length; i++)
        {
            TMP_Text text = GetNthTextMeshProText(components, i);
            if (initialization)
            {
                text.color = textController.GetDeselectedColor();
            }
            else
            {
                textController.ChangeColor(text);
            }
        }
    }

    // update GGO text color
    void UpdateGGOTextColor(GameObject ggo, bool initialization)
    {
        Component[] children = ggo.GetComponentsInChildren<PressableButtonHoloLens2>(false);
        UpdateComponentsTextColor(children, initialization);
    }

    void UpdateNthGGOTextColor(GameObject ggo, int nth, bool select)
    {
        if (textController == null) return;

        Component[] children = ggo.GetComponentsInChildren<PressableButtonHoloLens2>(false);
        if (nth >= children.Length) return;

        TMP_Text text = GetNthTextMeshProText(children, nth);

        textController.SetColor(text, select);
    }

    // Update the n-th object's color of GridGameObject
    void UpdateNthGGOTextColor(GameObject ggo, int nth)
    {
        if (textController == null) return;

        Component[] children = ggo.GetComponentsInChildren<PressableButtonHoloLens2>(false);
        if (nth >= children.Length) return;

        TMP_Text text = GetNthTextMeshProText(children, nth);
        textController.ChangeColor(text);
    }

    // Update the n-th button's color in objectGridGameObject's childeren
    void UpdateNthObjectTextColor(int nth)
    {

        UpdateNthGGOTextColor(objectGridGameObject, nth);
    }

    // Update the n-th button's color in 
    void UpdateNthOptionTextColor(int nth)
    {
        UpdateNthGGOTextColor(allObjectsGridGameObject, nth);
    }

    /// <summary>
    /// Get Selected Texts in Grid GameObject
    /// </summary>
    /// <param name="gridgo"></param>
    /// <param name="tc"></param>
    /// <returns></returns>
    List<string> GetSelectedTexts(GameObject gridgo, TextController tc)
    {
        List<string> names = null;

        if (tc == null) return names;

        names = new List<string>();

        Component[] children = gridgo.GetComponentsInChildren<PressableButtonHoloLens2>(false);
        for (int i = 0; i < children.Length; i++)
        {
            var text = GetNthTextMeshProText(children, i);
            if (text != null && textController.IsSelected(text))
            {
                names.Add(text.text);
            }
        }
        return names;
    }

    // Update keyword text color
    void UpdateNthKeywordTextColor(int nth)
    {
        UpdateNthGGOTextColor(keywordGridGameObject, nth);
    }

    // Get selected keywords, which means whose color is selected color
    public string GetSelectedKeywords()
    {
        var names = GetSelectedTexts(keywordGridGameObject, this.textController);

        if (names == null) return "";
        return string.Join(",", names);
    }

    // Get current object(s)
    // if rootKey not in ('x2', 'xn'), the object is seletedObject
    // if user touches any object, the object is selectedObject

    public string GetCurrentObject()
    {
        var selectedObjs = GetSelectedTexts(objectGridGameObject, this.textController);
        var selectedOpts = GetSelectedTexts(allObjectsGridGameObject, this.textController);

        var names = new List<string>();
        
        if (selectedObjs != null) names.AddRange(selectedObjs);
        if (selectedOpts != null) names.AddRange(selectedOpts);

        return String.Join(",", names);
    }

    // generate basename use timestamp
    public string GetBaseName()
    {
        return System.Guid.NewGuid().ToString();
    }

    // request sentences
    public void RequestSentences()
    {
        startTimestampMS = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        byte[] response = null;
        using (WebClientCert client = new WebClientCert())
        {
            var data = new NameValueCollection();
            data["basename"] = GetBaseName();
            data["object"] = GetCurrentObject();
            data["keywords"] = GetSelectedKeywords();
            data["voice"] = ButtonPressEvent.GetSwitchValue(voiceSwitcher, "female", "male", "male"); // default is male
            data["rate"] = ((int)ButtonPressEvent.NormalizeValue(ButtonPressEvent.GetSliderValue(voiceRateSlider, 0.5f), 100, 200)).ToString();
            data["volume"] = ButtonPressEvent.GetSliderValue(voiceVolumeSlider, 0.5f).ToString();

            try
            {
                buttonPressEvent.WriteRunLog(LogType.REQUEST, "");
                response = client.UploadValues(host + url, data);
                buttonPressEvent.WriteRunLog(LogType.TEXT_RES, "");
            }
            catch(WebException we)
            {
                // Debug.LogException(we);
                // show dialog
                if (dialogController != null)
                {
                    // Debug.LogError("show dialog");
                    dialogController.OpenConfirmationDialogMedium("Error", "Failed to generate sentences");
                }
            }
            HttpStatusCode code = client.StatusCode;
            string description = client.StatusDescription;
            if (code == HttpStatusCode.OK)
            {
                // 200
                //#if UNITY_EDITOR
                //                string debug_json = @"D:\holoAAC_test.json";
                //                File.WriteAllBytes(debug_json, response);
                //#endif
                
                // parse response, this operation will update UI
                buttonPressEvent.ParseWebResponse(response);
            }
        }
        long timeUsed = DateTimeOffset.Now.ToUnixTimeMilliseconds() - startTimestampMS;
        SetTimerText(timeUsed);
    }

    // ignore button action
    public void IgnoreAction()
    {
        // reset rootkey
        this.rootKey = null;
        // make a request
        RequestSentences();
    }

    // update frequency
    public void RequestUpdateFrequency(int index)
    {
        //get sentences
        string sentence = this.sentences[index];
        using (WebClientCert client = new WebClientCert())
        {
            var data = new NameValueCollection();
            data["sentence"] = sentence;

            try
            {
                // response is useless
                client.UploadValues(host + ufUrl, data);
            }
            catch (WebException we)
            {
                // Debug.LogException(we);
            }
            HttpStatusCode code = client.StatusCode;
            string description = client.StatusDescription;
            if (code == HttpStatusCode.OK)
            {
                // Debug.LogError("Frequency updated");
            }
        }
    }

}