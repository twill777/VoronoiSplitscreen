using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class SplitScreen : MonoBehaviour
{
    public Transform[] PlayerTransforms;

    private Vector3 midpoint;
    private float aspect;
    public int numPlayers = 2;

    public float circleRad;
    public float var1;
    public float var2;
    public float var3;
    public Color[] colors;

    Camera followCam;
    Camera mainCam;
    Camera overview;
    public ComputeShader splitShader;

    public RenderTexture screenRender;
    public RenderTexture player1Render;
    public RenderTexture player2Render;
    public RenderTexture player3Render;
    public RenderTexture resultRender;

    public RenderTexture overviewRender;
    public RenderTexture overviewResult;

    const float PI = 3.1415926538f;

    public struct Player
    {
        public int group;
        public float groupWeight;
        public Vector2 midPos;
        public Vector2 position;
        public Vector4 color;
    }

    public struct TexStruct
    {
        public RenderTexture tex;
    }

    private Player[] players;
    private Player[] groups;

    float bound;

    bool cameraStyle = true; // false = traditional
    int currentPlayer = 0;

    // Start is called before the first frame update
    void Start()
    {
        midpoint = Vector3.Lerp(PlayerTransforms[0].position, PlayerTransforms[1].position, 0.5f);

        mainCam = Camera.main;
        bound = mainCam.orthographicSize * var1;
        aspect = mainCam.aspect;

        followCam = transform.Find("FollowCam").GetComponent<Camera>();
        screenRender = new RenderTexture(Screen.width, Screen.height, 24);
        screenRender.enableRandomWrite = true;
        screenRender.Create();
        followCam.targetTexture = screenRender;

        overview = transform.Find("Overview").GetComponent<Camera>();
        overviewRender = new RenderTexture(Screen.width, Screen.height, 24);
        overviewRender.enableRandomWrite = true;
        overviewRender.Create();
        overview.targetTexture = overviewRender;

        resultRender = new RenderTexture(Screen.width, Screen.height, 24);
        resultRender.enableRandomWrite = true;
        resultRender.Create();

        overviewResult = new RenderTexture(Screen.width, Screen.height, 24);
        overviewResult.enableRandomWrite = true;
        overviewResult.Create();

        player1Render = new RenderTexture(Screen.width, Screen.height, 24);
        player1Render.enableRandomWrite = true;
        player1Render.Create();
        PlayerTransforms[0].GetComponent<PlayerController>().setRenderTarget(player1Render);

        player2Render = new RenderTexture(Screen.width, Screen.height, 24);
        player2Render.enableRandomWrite = true;
        player2Render.Create();
        PlayerTransforms[1].GetComponent<PlayerController>().setRenderTarget(player2Render);

        player3Render = new RenderTexture(Screen.width, Screen.height, 24);
        player3Render.enableRandomWrite = true;
        player3Render.Create();
        PlayerTransforms[2].GetComponent<PlayerController>().setRenderTarget(player3Render);

        //splitShader.SetTexture(0, "ScreenRender", screenRender);
        splitShader.SetTexture(0, "Render0", player1Render);
        splitShader.SetTexture(0, "Render1", player2Render); 
        splitShader.SetTexture(0, "Render2", player3Render); 
        splitShader.SetTexture(0, "Result", resultRender);
        splitShader.SetTexture(0, "Overview", overviewRender);
        splitShader.SetTexture(0, "OverviewResult", overviewResult);

        RenderTexture textures2DArray = new RenderTexture(Screen.width, Screen.height, 24);
        textures2DArray.dimension = TextureDimension.Tex2DArray;
        textures2DArray.enableRandomWrite = true;
        textures2DArray.volumeDepth = 3;
        textures2DArray.Create();    
        splitShader.SetTexture(0, "Textures", textures2DArray);

        splitShader.SetFloats("Resolution", new float[2] {screenRender.width, screenRender.height});
        splitShader.SetFloat("CircleRad", circleRad);
        splitShader.SetFloat("MaxDist", var1);
        splitShader.SetInt("NumPlayers", numPlayers);

        players = new Player[numPlayers];
        groups = new Player[numPlayers];

        for (int i = 0; i < numPlayers; i++)
        {
            players[i] = new Player();
            players[i].color = colors[i];
            players[i].position = TrimVec3(PlayerTransforms[i].position);
            players[i].groupWeight = 1f;
        }

        Buffer();
    }

    // Update is called once per frame
    void Update()
    {
        Buffer();
        UpdateVars();
        transform.position = midpoint;

        if (Input.GetKeyDown("c"))
        {
            cameraStyle = !cameraStyle;
            Debug.Log(cameraStyle);
        }

        if (Input.GetKeyDown("1") || Input.GetKeyDown("2") || Input.GetKeyDown("3"))
        {
            if (Input.GetKeyDown("1")) { currentPlayer = 0; }

            if (Input.GetKeyDown("2")) { currentPlayer = 1; }

            if (Input.GetKeyDown("3")) { currentPlayer = 2; }

            for (int i = 0; i < numPlayers; i++)
            {
                PlayerTransforms[i].GetComponent<PlayerController>().ChangePlayer(currentPlayer);
            }
        }
    }

    void UpdateVars()
    {
        float x = 0f;
        float y = 0f;
        float z = 0f;
        for (int i = 0; i < numPlayers; i++)
        {
            x += PlayerTransforms[i].position.x;
            y += PlayerTransforms[i].position.y;
            z += PlayerTransforms[i].position.z;
        }
        midpoint =  new Vector3(x / numPlayers, y / numPlayers, z / numPlayers);
        bound = mainCam.orthographicSize * var1;

        splitShader.SetFloats("Resolution", new float[2] {resultRender.width, resultRender.height});
        splitShader.SetFloat("CircleRad", circleRad);
        splitShader.SetFloat("MaxDist", var1);
        splitShader.SetInt("NumPlayers", numPlayers);
    }

    void Buffer()
    {
        ComputeBuffer playerBuffer = BufferPlayers();

        splitShader.Dispatch(0, screenRender.width / 8, screenRender.height / 8, 1);
        
        playerBuffer.Release();
    }

    Vector2 GetScreenPos(Vector2 vec)
    {
        vec.x = vec.x / (mainCam.orthographicSize * aspect);
        vec.y = vec.y / (mainCam.orthographicSize);

        return vec;
    }

    Vector3 GetPlayerCamPos(Vector2 vec)
    {
        Vector3 output = new Vector3();
        
        output.x = vec.x * mainCam.orthographicSize * aspect;
        output.y = vec.y * mainCam.orthographicSize;

        return output;
    }


    Vector2 ProcessDiff(Vector2 diff)
    {
        float xyDiff = Mathf.Abs(diff.x) - Mathf.Abs(diff.y); // >0 means x is greater

        float yDiff = Mathf.Clamp(xyDiff, 0, bound) / bound; // 0 when y > x, 1 when x >> y
        float xDiff = Mathf.Clamp(-xyDiff, 0, bound) / bound; // 0 when x > y, 1 when y >> x

        float lerp = diff.magnitude - bound;
        lerp = Mathf.Clamp(lerp, 0, bound) / bound; // 0 when diff.magnitude < bound, 1 when diff.magnitude > bound * 2

        diff.x = Mathf.Clamp(diff.x, -bound, bound);
        diff.y = Mathf.Clamp(diff.y, -bound, bound);

        diff.x -= diff.x * xDiff * lerp;
        diff.y -= diff.y * yDiff * lerp;

        return diff;
    }


    Vector2 GetPlayerCamOff(Player[] points, int myIdx)
    {
        Vector2 offset = new Vector2();
        for(int i = 0; i < points.Length; i++)
        {
            Vector2 diff = (points[i].position - points[myIdx].position);
            diff.x /= aspect;

            diff.Normalize();
            diff *= bound;

            diff.x  *= aspect;
            offset += diff;
        }

        offset /= numPlayers;
        return offset;
    }


    Vector2 GetPlayerCamOff2(Player[] points, int myIdx)
    {
        Vector2 offset = new Vector2();
        for(int i = 0; i < points.Length; i++)
        {
            Vector2 diff = (points[i].position - points[myIdx].position);
            diff.x /= aspect;

            if (diff.magnitude < bound)
            {
                diff.Normalize();
                diff *= bound;
            }

            diff.x = Mathf.Clamp(diff.x, -bound, bound);
            diff.y = Mathf.Clamp(diff.y, -bound, bound);

            diff.x  *= aspect;
            offset += diff;
        }

        offset /= numPlayers;
        return offset;
    }


    Vector2 GetPlayerOffset(Player[] points, int myIdx)
    {
        Vector2 offset = new Vector2();
        for(int i = 0; i < points.Length; i++)
        {
            Vector2 diff = (points[i].position - points[myIdx].position);
            diff.x /= aspect;

            if (diff.magnitude > bound)
            {
                diff.Normalize();
                diff *= bound;
            }

            diff.x  *= aspect;
            offset += diff;
        }

        offset /= numPlayers;
        return offset;
    }

   /*Vector2 GetPlayerOffset2(Player[] points, int myIdx)
    {
        Vector2 offset = new Vector2();
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 diff = (points[i].position - points[myIdx].position);
            Vector2 screenDiff = -(points[i].midPos - points[myIdx].midPos);

            diff.x /= aspect;
            screenDiff.x /= aspect;

            diff.x = Mathf.Clamp(diff.x, -Mathf.Abs(screenDiff.x), Mathf.Abs(screenDiff.x));
            diff.y = Mathf.Clamp(diff.y, -Mathf.Abs(screenDiff.y), Mathf.Abs(screenDiff.y));

            if (Mathf.Abs(diff.y) <= Mathf.Abs(screenDiff.y) && Mathf.Abs(diff.x) <= Mathf.Abs(screenDiff.x))
            {
                diff.y -= ((screenDiff.y - diff.y) / 2f) * var2;
                diff.x -= ((screenDiff.x - diff.x) / 2f) * var3;
            }

            diff.x *= aspect;
            offset += diff;
        }

        offset /= numPlayers;
        return offset;
    } */

    float EaseInCirc(float x) {
        return 1 - Mathf.Sqrt(1 - Mathf.Pow(x, 2));
    }

    float EaseInExpo(float x) {
        return x == 0 ? 0 : Mathf.Pow(2, 10 * x - 10);
    }

    Vector2 GetPlayerOffset2(Player[] points, int myIdx)
    {
        Vector2 offset = Vector2.zero;
        float count = 0;

        for (int i = 0; i < points.Length; i++)
        {
            Vector2 diff = (points[i].position - points[myIdx].position);
            Vector2 screenDiff = -(points[i].midPos - points[myIdx].midPos);

            diff.x /= aspect;
            screenDiff.x /= aspect;

            Vector2 currentOffset = count == 0 ? offset : offset / count;


            // When players are vertically stacked 1-2-3, and 2,3 are close together, as 1 (the top player) approaches it detects and is offset by 3 (the bottom player) before 2 (the middle player)
            /*if (myIdx == 0 && i == 2)
            {
                Debug.Log(Mathf.Abs(diff.y) <= Mathf.Min(bound, Mathf.Abs(screenDiff.y)));
            }*/
            
            if ((Mathf.Abs(diff.x) <= Mathf.Min(bound, Mathf.Abs(screenDiff.x))) && (Mathf.Abs(diff.y) <= Mathf.Min(bound, Mathf.Abs(screenDiff.y))))
            {
                if (Mathf.Abs(diff.x) <= Mathf.Abs(screenDiff.x))
                {
                    diff.x *= aspect;
                    screenDiff.x *= aspect;
                    
                    offset.x -= (screenDiff.x - diff.x);
                }   

                if (Mathf.Abs(diff.y) <= Mathf.Abs(screenDiff.y))
                {
                    
                    offset.y -= (screenDiff.y - diff.y);
                }

                count += 1f;
            }

            /*if (myIdx == 0 && i == 2)
            {
                Debug.Log(offset);
            }*/
        }

        offset /= count;
        offset += points[myIdx].midPos;
        return offset;
    }
    
    ComputeBuffer BufferPlayers()
    {
        for (int i = 0; i < numPlayers; i++)
        {
            players[i].group = i;
            players[i].color = colors[i];
            players[i].position = TrimVec3(PlayerTransforms[i].position);
            players[i].midPos = players[i].position;
            players[i].groupWeight = 1f;

            groups[i].position = players[i].position;
            groups[i].midPos = new Vector2();
            groups[i].groupWeight = 1f;
            groups[i].group = i;
        }

        for (int i = 0; i < numPlayers; i++)
        {
            if(!cameraStyle)
            {
                groups[i].position = GetPlayerCamOff(players, i);
            } else {
                groups[i].position = GetPlayerCamOff2(players, i);
            }
            players[i].midPos = groups[i].position;
        }

        for (int i = 0; i < numPlayers; i++)
        {
            if(!cameraStyle)
            {
                groups[i].midPos = GetPlayerOffset(players, i);
            } else {
                groups[i].midPos = GetPlayerOffset2(players, i);
            }
        }

        for (int i = 0; i < numPlayers; i++)
        {
            // Shift the player's camera from midPos by camPos
            PlayerTransforms[i].GetComponent<PlayerController>().MoveCamera(groups[i].midPos);

            // Screenspace vector from player's position->camPos
            players[i].position = GetScreenPos(groups[i].position);

            // Screenspace vector from player's midPos->camPos
            players[i].midPos = GetScreenPos(groups[i].midPos);
        }

        splitShader.SetInt("NumPlayers", numPlayers);

        ComputeBuffer playerBuffer = new ComputeBuffer(players.Length, sizeof(int) + sizeof(float) * (1 + 2 + 2 + 4));
        playerBuffer.SetData(players);
        splitShader.SetBuffer(0, "players", playerBuffer);

        return playerBuffer;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        Graphics.Blit(resultRender, dest);
        //Graphics.Blit(overviewResult, dest);
    }

    Vector2 TrimVec3(Vector3 input)
    {
        Vector2 output = new Vector2(input.x, input.y);
        return output;
    }
}
