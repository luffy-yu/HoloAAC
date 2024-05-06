using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Leguar.TotalJSON;
using Microsoft.MixedReality.Toolkit.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Windows.WebCam;

public enum LogType
{
    OPEN = 1,
    //CLICK = 2,
    CLOSE = 3,

    CLICK_CAM = 20,
    CLICK_OBJ = 21,
    CLICK_KYW = 22,
    CLICK_STS = 23,
    CLICK_IGN = 24, // ignore
    CLICK_BAK = 25, // back

    CLICK_OPT = 26, // options

    REQUEST = 30, // indicate when to request
    IMAGE_RES = 31, // get response from image request
    TEXT_RES = 32, // get response from text request

    TIME_COST = 40, // time cost in request

    IMG_SAVE = 50, // image save
};

/// <summary>
/// An expanded web client that allows certificate auth and 
/// the retrieval of status' for successful requests
/// </summary>
public class WebClientCert : WebClient
{
    private X509Certificate2 _cert;
    public WebClientCert(X509Certificate2 cert) : base() { _cert = cert; }

    public WebClientCert() : base()
    {
        _cert = null;
    }

    protected override WebRequest GetWebRequest(Uri address)
    {
        HttpWebRequest request = (HttpWebRequest)base.GetWebRequest(address);
        request.Timeout = 1000 * 1000;// 1000 second
        request.ReadWriteTimeout = 1000 * 1000;
        if (_cert != null) { request.ClientCertificates.Add(_cert); }
        return request;
    }
    protected override WebResponse GetWebResponse(WebRequest request)
    {
        WebResponse response = null;
        response = base.GetWebResponse(request);
        HttpWebResponse baseResponse = response as HttpWebResponse;
        StatusCode = baseResponse.StatusCode;
        StatusDescription = baseResponse.StatusDescription;
        return response;
    }
    /// <summary>
    /// The most recent response statusCode
    /// </summary>
    public HttpStatusCode StatusCode { get; set; }
    /// <summary>
    /// The most recent response statusDescription
    /// </summary>
    public string StatusDescription { get; set; }
}

// refer: https://stackoverflow.com/questions/51711837/hololens-no-capture-device-found-error
public class ButtonPressEvent : MonoBehaviour
{
    // button references
    // camera button: detect object, and then generate sentences
    [Tooltip("Camera button in hand menu")]
    [SerializeField] private GameObject cameraButton1;

    // ignore button: bypass detecting object,
    // enter the second UI directly,
    // use Select Object window to detect window,
    [Tooltip("Ignore button in hand menu")]
    [SerializeField] private GameObject ignoreButton;

    [Tooltip("Button in speak menu")]
    [SerializeField] private GameObject cameraButton2;

    [Tooltip("Back button in speak menu")]
    [SerializeField] private GameObject backButton;

    [Tooltip("Close button in speak menu")]
    [SerializeField] private GameObject closeButton;

    // call from SettingsButtonPressEvent
    // service host and url
    [Tooltip("The host to call python api")]
    private string webServiceHost;

    [Tooltip("The url to call, excluding host, startswith /")]
    [SerializeField] private string webServicePath;

    // voice switcher
    [Tooltip("Voice switch in hand menu")]
    [SerializeField] private GameObject voiceSwitcher;

    [Tooltip("Voice rate slider in hand menu")]
    [SerializeField] private GameObject voiceRateSlider;

    [Tooltip("Voice volume slider in hand menu")]
    [SerializeField] private GameObject voiceVolumeSlider;

    [Tooltip("HandMenu")]
    [SerializeField] private GameObject handMenu;

    [Tooltip("Speak menu when request is done")]
    [SerializeField] private GameObject speakMenu;

    // menu controller
    [Tooltip("Menu controller reference")]
    [SerializeField] private MenuController menuController;

    // title of object window
    // set it to "Selected Object" when press ignore, otherwise set it to "Detected Objects"
    [Tooltip("Title of detected object window")]
    [SerializeField] private TMP_Text detectedObjectText;

    // debug
    [Tooltip("Debug Image Path on Windows File system")]
    [SerializeField] private string debugImagePath;

    PhotoCapture photoCaptureObject = null;
    Texture2D targetTexture = null;

    string imageFilename = null;

    private byte[] imgData;
    private int width;
    private int height;

    // timestamp for counting requests used time
    private long startTimestampMS = 0;

    void Start()
    {
        // write log
        WriteRunLog(LogType.OPEN, "");
        
        // set visiablity when starting
        setVisibility(true, false);

        // camera button event, change title first
        SetButtonEvent(cameraButton1, delegate { WriteRunLog(LogType.CLICK_CAM, ""); OnCameraButtonClicked(); });
        SetButtonEvent(cameraButton2, delegate { WriteRunLog(LogType.CLICK_CAM, ""); OnCameraButtonClicked(); });

        // ignore button event, change title first
        SetButtonEvent(ignoreButton, delegate { WriteRunLog(LogType.CLICK_IGN, ""); OnIgnoreButtonClicked(); });

        // back button event
        SetButtonEvent(backButton, delegate { WriteRunLog(LogType.CLICK_BAK, ""); OnBackButtonClicked(); });

        // close button event
        SetButtonEvent(closeButton, delegate { WriteRunLog(LogType.CLOSE, ""); OnCloseButtonClicked(); });
    }

    public static float GetSliderValue(GameObject go, float @default)
    {
        if (go == null) return @default;

        PinchSlider ps = null;
        go.TryGetComponent<PinchSlider>(out ps);
        
        if (ps == null) return @default;
        
        return ps.SliderValue;
    }

    public static float NormalizeValue(float value, float min, float max)
    {
        return (max - min) * value + min;
    }

    public static string GetSwitchValue(GameObject go, string true_value, string false_value, string default_value)
    {
        if (go == null) return default_value;

        Interactable interactable = null;

        go.TryGetComponent<Interactable>(out interactable);

        if (interactable == null) return default_value;

        if (interactable.IsToggled) return true_value;
        else return false_value;
    }

    public static void SetButtonEvent(GameObject button, UnityAction call)
    {
        ButtonConfigHelper bch = null;
        button.TryGetComponent<ButtonConfigHelper>(out bch);
        bch.OnClick.RemoveAllListeners();
        bch.OnClick.AddListener(call);
    }

    public void OnBackButtonClicked()
    {
        // reset UI first
        if(menuController != null)
        {
            menuController.ResetUI();
        }
        // reset content
        setVisibility(true, false);
    }

    public void OnCloseButtonClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
        Application.Quit();
    }

    // ignore button press event
    public void OnIgnoreButtonClicked()
    {
        // request without image
        menuController.IgnoreAction();
    }

    public void OnCameraButtonClicked()
    {
        // reset Options window
        menuController.ResetOptionsPanel();

#if UNITY_EDITOR
        // Debug in Unity Editor
        CallWebAPI();
        return;
#endif
        // Debug.Log("Get texture and resolution");
        Resolution cameraResolution = PhotoCapture.SupportedResolutions.OrderByDescending((res) => res.width * res.height).First();
        targetTexture = new Texture2D(cameraResolution.width, cameraResolution.height);
        // Create a PhotoCapture object
        PhotoCapture.CreateAsync(false, delegate (PhotoCapture captureObject)
        {
            // Debug.Log("Starting photo capture");
            photoCaptureObject = captureObject;
            CameraParameters cameraParameters = new CameraParameters();
            cameraParameters.hologramOpacity = 0.0f;
            cameraParameters.cameraResolutionWidth = cameraResolution.width;
            cameraParameters.cameraResolutionHeight = cameraResolution.height;
            cameraParameters.pixelFormat = CapturePixelFormat.BGRA32;
            width = cameraResolution.width;
            height = cameraResolution.height;
            // Debug.Log("Begin taking photo");
            // Activate the camera
            photoCaptureObject.StartPhotoModeAsync(cameraParameters, delegate (PhotoCapture.PhotoCaptureResult result)
            {
                // Take a picture
                var dt = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                try
                {

                    //#if !UNITY_EDITOR && UNITY_WSA
                    //                    string filePath = System.IO.Path.Combine(Windows.Storage.KnownFolders.PicturesLibrary.Path, filename);
                    //#else
                    //string filePath = System.IO.Path.Combine(Application.persistentDataPath, filename);
                    //#endif

                    imageFilename = getImageFilename(dt);
                    photoCaptureObject.TakePhotoAsync(imageFilename, PhotoCaptureFileOutputFormat.JPG, OnCapturedPhotoToDisk);
                    //photoCaptureObject.TakePhotoAsync(OnCapturedPhotoToMemory);
                }
                catch (Exception e)
                {
                    // Debug.Log(e.StackTrace);
                }
            });
        });
    }

    string getImageFilename(String dt)
    {
        var filename = string.Format(@"IMG_{0}.jpg", dt);
        string filePath = System.IO.Path.Combine(Application.persistentDataPath, filename);
        return filePath;
    }

    string getMP3Filename(string dt)
    {
        var filename = string.Format(@"IMG_{0}.mp3", dt);
        string filePath = System.IO.Path.Combine(Application.persistentDataPath, filename);
        return filePath;
    }

    public static string getFullPath(string filename)
    {
#if UNITY_EDITOR
        return @"D:\" + filename;
#endif
        return System.IO.Path.Combine(Application.persistentDataPath, filename);
    }

    string getLogFilename()
    {
        var dt = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var filename = string.Format(@"log_{0}.txt", dt);
        string filePath = System.IO.Path.Combine(Application.persistentDataPath, filename);
        return filePath;
    }

    // write log for analysis
    public void WriteRunLog(LogType lt, string message)
    {
        var date = DateTime.Now.ToString("yyyyMMdd");
        var filename = string.Format(@"data_{0}.txt", date);
        
        string filepath = "";
        filepath = System.IO.Path.Combine(Application.persistentDataPath, filename);
#if UNITY_EDITOR
        filepath = @"D:\" + filename;
#endif
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        using (TextWriter writer = File.AppendText(filepath))
        {
            // write text
            writer.WriteLine(string.Format(@"{0},{1},{2}", timestamp, lt, message));
        }
    }

    void OnStoppedPhotoMode(PhotoCapture.PhotoCaptureResult result)
    {
        photoCaptureObject.Dispose();
        photoCaptureObject = null;
    }

    void OnCapturedPhotoToDisk(PhotoCapture.PhotoCaptureResult result)
    {
        if (result.success)
        {
            // write log
            WriteRunLog(LogType.IMG_SAVE, System.IO.Path.GetFileName(imageFilename));
            //Debug.Log("Saved Photo to disk!");
            try
            {
                CallWebAPI();
            } catch (Exception ecp)
            {
                string path = getLogFilename();
                using (TextWriter writer = File.CreateText(path))
                {
                    // write text
                    writer.WriteLine(ecp.ToString());
                }
            }
            photoCaptureObject.StopPhotoModeAsync(OnStoppedPhotoMode);
        }
        else
        {
            //Debug.Log("Failed to save Photo to disk");
            imageFilename = null;
        }
    }

    void CallWebAPI()
    {
#if UNITY_EDITOR
        //imageFilename = @"D:\water_detected.png";
        imageFilename = debugImagePath;
        //imageFilename = @"D:\x2.png";
        webServiceHost = "http://127.0.0.1:8000";
#endif
        // Debug.Log("Call web api");
        if(imageFilename == null)
        {
            //Debug.LogError("Due to the failure to save image, can not execute call web api");
            return;
        }
        var data = ImageToBase64(imageFilename);
        var url = SettingsButtonPressEvent.GetServiceHost() + webServicePath;
        PostImageBase64DataNew(data, url);
    }

    public string ImageToBase64(string filepath)
    {
        byte[] imageArray = System.IO.File.ReadAllBytes(filepath);
        string base64ImageRepresentation = Convert.ToBase64String(imageArray);
        return base64ImageRepresentation;
    }

    public static byte[] Base64StringToBase64(string data)
    {
        return Convert.FromBase64String(data);
    }

    void CallWebAPITest()
    {
        var url = "http://204.79.197.200/";
        using (WebClientCert client = new WebClientCert())
        {
            byte[] response = client.DownloadData(url);
            Debug.Log(response.Length);
        }
    }

    public void PostImageBase64DataNew(string image, string url)
    {
        startTimestampMS = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        byte[] response = null;
        using (WebClientCert client = new WebClientCert())
        {
            var data = new NameValueCollection();
            data["data"] = image;
            data["filename"] = System.IO.Path.GetFileName(imageFilename);
            data["voice"] = GetSwitchValue(voiceSwitcher, "female", "male", "male"); // default is male
            data["rate"] = ((int)NormalizeValue(GetSliderValue(voiceRateSlider, 0.5f), 50, 200)).ToString();
            data["volume"] = GetSliderValue(voiceVolumeSlider, 0.5f).ToString();

            try
            {
                WriteRunLog(LogType.REQUEST, "");
                response = client.UploadValues(url, data);
                WriteRunLog(LogType.IMAGE_RES, "");
            }
            catch (WebException we)
            {
                //Debug.LogException(we);
                // e.g., 400
                if (menuController != null)
                {
                    menuController.GetDialogController().OpenConfirmationDialogMedium("Error", "Failed to detect object");
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

                ParseWebResponse(response);
            }
        }
        long timeUsed = DateTimeOffset.Now.ToUnixTimeMilliseconds() - startTimestampMS;
        if(menuController != null)
        {
            menuController.SetTimerText(timeUsed);
        }
    }
    public void ParseWebResponse(byte[] response)
    {
        Encoding encoding = Encoding.UTF8;
        string s = encoding.GetString(response);
        
        // parse json
        JSON json = JSON.ParseString(s);
        
        string[] ogg_filenames = new string[] { };
        string[] ogg_data = new string[] { };

        string rootKey = "";

        string[] objects = new string[] { };
        string[] keywords = new string[] { };
        string[] sentences = new string[] { };
        // overWriteObjects
        bool owo = true;

        // de-jsonify
        foreach (string key in json.Keys)
        {
            if(key == "ogg_filenames")
            {
                JArray array = json.GetJArray(key);
                ogg_filenames = array.AsStringArray();
            }
            else if(key == "ogg_data")
            {
                JArray array = json.GetJArray(key);
                ogg_data = array.AsStringArray();
            }
            else if(key == "overwrite_objects")
            {
                owo = json.GetBool("overwrite_objects");
            }
            else
            {
                rootKey = key;
                // fill data          
                JSON data = json.GetJSON(key);
                objects = data.GetJArray("objects").AsStringArray();
                keywords = data.GetJArray("keywords").AsStringArray();
                sentences = data.GetJArray("sentences").AsStringArray();
            }
        }

        SaveOggFiles(ogg_filenames, ogg_data);
        // update UI
        if (menuController != null)
        {
            menuController.UpdateVariables(rootKey, objects, keywords, sentences, ogg_filenames, owo);
        }
        // switch UI
        setVisibility(false, true);
    }

    public static void SaveOggFiles(string[] filenames, string [] data)
    {
        for(int i = 0;i < filenames.Length; i++)
        {
            string filename = getFullPath(filenames[i]);
            File.WriteAllBytes(filename, Base64StringToBase64(data[i]));
        }
    }

    public void setVisibility(bool hand, bool speak)
    {
        if(handMenu != null)
        {
            handMenu.SetActive(hand);
        }

        if(speakMenu != null)
        {
            speakMenu.SetActive(speak);
        }

        // update timer
        if (menuController != null)
        {
            // show only when in speak ui
            menuController.GetTimer().enabled = speak == true;
        }
    }

}