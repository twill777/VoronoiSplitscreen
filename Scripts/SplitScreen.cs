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
        splitShader.SetFloat("CurveFactor", var2);
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

        splitShader.SetFloat("CurveFactor", var2);
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

        offset = (offset / numPlayers);

        Vector2 midDiff = (TrimVec3(midpoint) - points[myIdx].position);

        //float lerp = Mathf.Max(0f, 1f - Mathf.Max(0f,((var3 * worldDiff.magnitude) - screenDiff.magnitude) / screenDiff.magnitude))

        if (midDiff.magnitude < offset.magnitude)
        {
            offset = midDiff;
            Debug.Log(midDiff);
        }

        return offset;
    }

    Vector2 GetPlayerOffset(Player[] points, Player[] groups, int myIdx)
    {
        Vector2 worldCoord = players[myIdx].position;
        Vector2 screenCoord = players[myIdx].midPos;

        Vector2 offset = new Vector2();

        float count = 0f;

        for (int i = 0; i < numPlayers; i++)
        {
            if (i == myIdx) { continue; }

            Vector2 worldOther = players[i].position;
            Vector2 screenOther = players[i].midPos;

            Vector2 worldDiff = worldOther - worldCoord;
            Vector2 screenDiff = screenOther - screenCoord;


            if (worldDiff.magnitude < screenDiff.magnitude)
            {
                offset += (worldDiff + screenDiff);
                count += 1f;
            }
        }
        
        if (count > 0f) 
        {
            offset /= count + 1f;
        }

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
            groups[i].position = GetPlayerCamOff(players, i);
            players[i].midPos = groups[i].position;
        }

        for (int i = 0; i < numPlayers; i++)
        {
            groups[i].midPos = GetPlayerOffset(players, groups, i);
        }

        for (int i = 0; i < numPlayers; i++)
        {
            // players[i].position        : player's world position
            //      Renders player dot at this vector from screen center

            // players[i].midPos          : player's world position, shifted to account for other close players
            //      Renders midpoint dot at this vector from screen center

            // groups[i].midPos           : average direction of all player directions relative to this player, times 'bound'
            //                            ^ NOTE - This is the "spread out" worldspace position the player's camera 
            //                                     from the center of the screen 
            //                                     (ex. 2 other players to left means camera is 2/3 * screen_width left of player)
            
            // Vec2 GetScreenPos(Vec2)    : Converts a Vec2 to screenspace coordinates
            // Vec3 GetPlayerCamPos(Vec2) : Converts a Vec2 to worldspace coordinates
            // Void MoveCamera(Vec3)      : Moves the camera to the player's worldspace position + Vec3 (in worldspace)

            // Shift the player's camera from midPos by camPos
            PlayerTransforms[i].GetComponent<PlayerController>().MoveCamera(groups[i].position + groups[i].midPos);

            // Screenspace vector from player's position->camPos
            players[i].position = GetScreenPos(groups[i].position);

            // Screenspace vector from player's midPos->camPos
            players[i].midPos = GetScreenPos(groups[i].position + groups[i].midPos);
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
