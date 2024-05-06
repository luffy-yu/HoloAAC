using System.IO;
using System.Net;
using Microsoft.MixedReality.Toolkit.Experimental.UI;
using TMPro;
using UnityEngine;

public class SettingsButtonPressEvent : MonoBehaviour
{
    [Tooltip("Setting button in start page")]
    [SerializeField] private GameObject settingButton;

    [Tooltip("Default button in settings page")]
    [SerializeField] private GameObject defaultButton;

    [Tooltip("Clear button in settings page")]
    [SerializeField] private GameObject clearButton;

    [Tooltip("Test button in settings page")]
    [SerializeField] private GameObject testButton;

    [Tooltip("Confirm button in settings page")]
    [SerializeField] private GameObject confirmButton;


    [Tooltip("Root GameObject of setting page")]
    [SerializeField] private GameObject rootSettings;

    [Tooltip("Input field")]
    [SerializeField] private MRTKTMPInputField inputField;

    [Tooltip("Status Text")]
    [SerializeField] private TMP_Text statusText;

    // default ip and port
    private static string defaultIPandPort = "169.254.176.4:8000";

    // save ip config file in disk
    private static string ipConfigFile = "host.ip";

    // Start is called before the first frame update
    void Start()
    {
        // set buttons' press event
        ButtonPressEvent.SetButtonEvent(settingButton, delegate { OnSettingButtonPressed(); });
        ButtonPressEvent.SetButtonEvent(defaultButton, delegate { OnDefaultButtonPressed(); });
        ButtonPressEvent.SetButtonEvent(clearButton, delegate { OnClearButtonPressed(); });
        ButtonPressEvent.SetButtonEvent(testButton, delegate { OnTestButtonPressed(); });
        ButtonPressEvent.SetButtonEvent(confirmButton, delegate { OnConfirmButtonPressed(); });
        // init show text
        inputField.text = ReadConfig();
    }

    static string GetIpConfigFile()
    {
        return ButtonPressEvent.getFullPath(ipConfigFile);
    }

    public static string GetServiceHost()
    {
        var host = "http://";
        string path = GetIpConfigFile();
        if (File.Exists(path))
        {
            // read file
            string text = File.ReadAllText(path).Trim();
            host += text;
        }
        else
        {
            // write default config and return default config
            WriteConfig(defaultIPandPort);
            host += defaultIPandPort;
        }
        // Debug.LogError("GetServiceHost: " + host);
        return host;
    }

    public string ReadConfig()
    {
        string path = GetIpConfigFile();
        if (File.Exists(path))
        {
            // read file
            string text = File.ReadAllText(path).Trim();
            // Debug.LogError("ReadConfig: " + text);
            return text;
        }
        else
        {
            // write default config and return default config
            WriteConfig(defaultIPandPort);
            return defaultIPandPort;
        }
    }

    static void WriteConfig(string config)
    {
        using (TextWriter writer = File.CreateText(GetIpConfigFile()))
        {
            // write text
            writer.WriteLine(config);
        }
    }

    void OnSettingButtonPressed()
    {
        // Debug.LogError("OnSettingButtonPressed");
        // update shown text
        inputField.text = ReadConfig();

        rootSettings.SetActive(true);
    }

    void OnDefaultButtonPressed()
    {
        // Debug.LogError("OnDefaultButtonPressed");
        inputField.text = defaultIPandPort;
    }

    void OnClearButtonPressed()
    {
        // Debug.LogError("OnClearButtonPressed");
        inputField.text = "";
    }

    void OnTestButtonPressed()
    {
        // Debug.LogError("OnTestButtonPressed");
        if(NetworkTest())
        {
            statusText.text = "OK";
        } else
        {
            statusText.text = "FAIL";
        }
    }

    bool NetworkTest()
    {
        var url = "http://" + inputField.text;
        using (WebClientCert client = new WebClientCert())
        {
            byte[] response = null;
            try
            {
                response = client.DownloadData(url);
            }
            catch (WebException we)
            {
                if (we.Status == WebExceptionStatus.ProtocolError)
                {
                    HttpStatusCode code = ((HttpWebResponse)we.Response).StatusCode;
                    // for the index page, it should return an 404 error
                    if(code == HttpStatusCode.NotFound)
                    {
                        return true;
                    }
                    else
                    {
                        // Debug.LogException(we);
                    }
                }
                else
                {
                    // Debug.LogException(we);
                }
            }
        }
        return false;
    }

    void OnConfirmButtonPressed()
    {
        // Debug.LogError("OnConfirmButtonPressed");
        
        // write file
        WriteConfig(inputField.text);

        rootSettings.SetActive(false);
    }

}