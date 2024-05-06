using System;
using UnityEngine;

public class DialogPositionController : MonoBehaviour
{
    [Tooltip("Which object to follow")]
    [SerializeField] private string followObjectPath;

    [Tooltip("Z offset relative to following object")]
    [SerializeField] private float zOffset = -0.01f;

    private GameObject followingObject = null;

    // round number to stabilize 
    private int numberRound = 4;

    private int forwardRound = 2;

    // Start is called before the first frame update
    void Start()
    {
        if (followObjectPath == null) return;
        followingObject = GameObject.Find(followObjectPath);
        if(followingObject != null)
            Debug.LogError("Find following object");
    }

    private Vector3 RoundVector3(Vector3 vec, int precision)
    {
        Vector3 result;
        result.x = (float)Math.Round(vec.x, precision);
        result.y = (float)Math.Round(vec.y, precision);
        result.z = (float)Math.Round(vec.z, precision);
        return result;
    }

    // Update is called once per frame
    void Update()
    {
        if (followingObject == null) return;

        double z = followingObject.transform.position.z;
        // set offset
        z += zOffset;
        // stabilize position through round number
        z = Math.Round(z, numberRound);
        // current position
        var position = transform.position;
        position.z = (float)z;
        transform.position = position;
        // update forward direction
        transform.forward = followingObject.transform.forward;
    }
}