using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private int facing = 1; // 0 for left, 1 for right

    private float speed = 3.5f;
    private Transform spriteTransform;
    private Camera playerCam;
    private RenderTexture targetTexture;

    public int playerNum;

    public int currentPlayer = 0;

    // Start is called before the first frame update
    void Start()
    {
        spriteTransform = transform.Find("PlayerSprite");
        playerCam = transform.Find("PlayerCamera").GetComponent<Camera>();
    }

    public void setRenderTarget(RenderTexture target)
    {
        targetTexture = target;
    }

    public void MoveCamera (Vector3 offset)
    {
        playerCam.transform.position = transform.position + offset - new Vector3(0f, 0f, 10f);
    }

    // Update is called once per frame
    void Update()
    {
        if (targetTexture && playerCam)
        {
            playerCam.targetTexture = targetTexture;
            targetTexture = null;
        }

        if (playerNum == currentPlayer) {
            float h = Input.GetAxis("Horizontal");

            if (h > 0) {
                facing = 1;
            } else if (h < 0) {
                facing = 0;
            }

            spriteTransform.localRotation = Quaternion.Euler(0, 180 * (1- facing), 0);

            Vector3 move = new Vector3(h, Input.GetAxis("Vertical"), 0);
            transform.position += move * speed * Time.deltaTime;
        }

        if (Input.GetKeyDown("1"))
        {
            currentPlayer = 0;
        }

        if (Input.GetKeyDown("2"))
        {
            currentPlayer = 1;
        }

        if (Input.GetKeyDown("3"))
        {
            currentPlayer = 2;
        }
    }
}
