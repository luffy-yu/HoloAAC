using System;
using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Networking;

public struct UnityWebRequestAwaiter : INotifyCompletion
{
    private UnityWebRequestAsyncOperation asyncOp;
    private Action continuation;

    public UnityWebRequestAwaiter(UnityWebRequestAsyncOperation asyncOp)
    {
        this.asyncOp = asyncOp;
        continuation = null;
    }

    public bool IsCompleted { get { return asyncOp.isDone; } }

    public void GetResult() { }

    public void OnCompleted(Action continuation)
    {
        this.continuation = continuation;
        asyncOp.completed += OnRequestCompleted;
    }

    private void OnRequestCompleted(AsyncOperation obj)
    {
        continuation?.Invoke();
    }
}

public static class ExtensionMethods
{
    public static UnityWebRequestAwaiter GetAwaiter(this UnityWebRequestAsyncOperation asyncOp)
    {
        return new UnityWebRequestAwaiter(asyncOp);
    }
}
public class MusicLoader : MonoBehaviour
{
    // Audio mixer
    [Tooltip("AutoMixer for button press sound event")]
    [SerializeField] private AudioMixerGroup audioMixerGroup;
    
    //AudioSource audioSource;
    // Start is called before the first frame update
    void Start()
    {
        //audioSource = gameObject.GetComponent<AudioSource>();

        //audioSource.outputAudioMixerGroup = audioMixerGroup;

        //audioSource.Play();


        //path = "file://D://Projects/Code/HoloAACServer/HoloAAC/TTS/test2.mp3";
        //w = new WWW(path);

        //string path = "D://Study/Github/HoloAACServer/HoloAAC/TTS/example/how_are_you.ogg";
        //StartCoroutine(LoadMusic(audioSource, path));
    }

    public AudioMixerGroup GetAudioMixerGroup()
    {
        return audioMixerGroup;
    }

    //// simple way
    //IEnumerator loadLocalMusic(string filePath)
    //{
    //    if (System.IO.File.Exists(filePath))
    //    {
    //        WWW www = new WWW("file://" + filePath);
    //        yield return www;
    //        audioSource.clip = www.GetAudioClip();
    //        audioSource.Play();
    //    }
    //}

    // refer: https://forum.unity.com/threads/load-mp3-files-saved-locally-to-audioclip.851434/#post-5616211
    public IEnumerator LoadMusic(AudioSource audioSource, string songPath, Action<int> callback, int index)
    {
        // Debug.LogError("IEnumerator start");
        UriBuilder builder = new UriBuilder(songPath);
        builder.Scheme = "file";
        if (System.IO.File.Exists(songPath))
        {
            using (var uwr = UnityWebRequestMultimedia.GetAudioClip(builder.ToString(), AudioType.OGGVORBIS))
            {
                ((DownloadHandlerAudioClip)uwr.downloadHandler).streamAudio = true;

                yield return uwr.SendWebRequest();

                if (uwr.isNetworkError || uwr.isHttpError)
                {
                    // Debug.LogError(uwr.error);
                    yield break;
                }

                DownloadHandlerAudioClip dlHandler = (DownloadHandlerAudioClip)uwr.downloadHandler;

                if (dlHandler.isDone)
                {
                    AudioClip audioClip = dlHandler.audioClip;

                    if (audioClip != null)
                    {
                        AudioClip _audioClip = DownloadHandlerAudioClip.GetContent(uwr);

                        // Debug.Log("Playing song using Audio Source!");

                        audioSource.clip = _audioClip;
                        audioSource.loop = false;
                        audioSource.Play();
                        yield return new WaitForSeconds(_audioClip.length);
                        callback(index);
                    }
                    else
                    {
                        // Debug.Log("Couldn't find a valid AudioClip :(");
                    }
                }
                else
                {
                    // Debug.Log("The download process is not completely finished.");
                }
            }
        }
        else
        {
            // Debug.Log("Unable to locate converted song file.");
        }
    }
}