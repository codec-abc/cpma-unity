/**
 * 
 * Adapted from https://raw.githubusercontent.com/Zinglish/quake3-movement-unity3d/master/CPMPlayer.js
 * 
 * 
**/

using UnityEngine;
using System.Linq;

public class player_cpma : MonoBehaviour
{
    /**
     * Just some side notes here.
     *
     * - Should keep in mind that idTech's cartisian plane is different to Unity's:
     *    Z axis in idTech is "up/down" but in Unity Z is the local equivalent to
     *    "forward/backward" and Y in Unity is considered "up/down".
     *
     * - Code's mostly ported on a 1 to 1 basis, so some naming convensions are a
     *   bit fucked up right now.
     *
     * - UPS is measured in Unity units, the idTech units DO NOT scale right now.
     *
     * - Default values are accurate and emulates Quake 3's feel with CPM(A) physics.
     */

    /* Player view stuff */

    [SerializeField]
    Transform jumpSoundsObj;
    JumpSoundsProvider jumpSoundsProvider;

    [SerializeField]
    Transform playerView;  // Must be a camera

    float xMouseSensitivity = 10.0f;
    float yMouseSensitivity = 10.0f;

    /* Frame occuring factors */
    float gravity = 20.0f;
    float friction = 50f;  // Ground friction

    /* Movement stuff */
    float moveSpeed = 50.0f;  // Ground move speed
    float runAcceleration = 14f;   // Ground accel
    float runDeacceleration = 10f;   // Deacceleration that occurs when running on the ground

    float airAcceleration = 2.0f;  // Air accel
    float airDeacceleration = 2.0f;    // Deacceleration experienced when opposite strafing
    float airControl = 0.3f;  // How precise air control is

    float sideStrafeAcceleration = 50f;   // How fast acceleration occurs to get up to sideStrafeSpeed when side strafing
    float sideStrafeSpeed = 1f;    // What the max speed to generate when side strafing

    float jumpSpeed = 8.0f;  // The speed at which the character's up axis gains when hitting jump
    float moveScale = 1.0f;

    /* FPS Stuff */
    float fpsDisplayRate = 4.0f;  // 4 updates per sec.

    private int frameCount = 0;
    private float dt = 0.0f;

    [SerializeField]
    private CharacterController controller;

    // Camera rotationals
    private float rotX = 0.0f;
    private float rotY = 0.0f;

    private Vector3 playerVelocity = Vector3.zero;
    private float playerTopVelocity = 0.0f;

    // Q3: players can queue the next jump just before he hits the ground
    private bool wishJump = false;

    // Contains the command the user wishes upon the character
    class Cmd
    {
        public float forwardmove;
        public float rightmove;
        public float upmove;
    }

    private Cmd cmd; // Player commands, stores wish commands that the player asks for (Forward, back, jump, etc)

    private void Start()
    {
        /* Hide the cursor */
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        controller = GetComponent<CharacterController>();
        cmd = new Cmd();

        var instanciated = Instantiate(jumpSoundsObj, Vector3.zero, Quaternion.identity);
        instanciated.parent = this.transform;
        jumpSoundsProvider = instanciated.GetComponent<JumpSoundsProvider>();
        Debug.Log(jumpSoundsProvider);
        Debug.Assert(jumpSoundsProvider != null);
    }

    private void FixedUpdate()
    {
        /* Do FPS calculation */
        frameCount++;
        dt += Time.deltaTime;

        if (dt > 1.0f / fpsDisplayRate)
        {
            frameCount = 0;
            dt -= 1.0f / fpsDisplayRate;
        }

        /* Ensure that the cursor is locked into the screen */
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (Input.GetMouseButtonDown(0))
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
        }
        else
        {
            if (Input.GetKey(KeyCode.Escape))
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        /* Camera rotation stuff, mouse controls this shit */
        rotX -= Input.GetAxis("Mouse Y") * xMouseSensitivity;
        rotY += Input.GetAxis("Mouse X") * yMouseSensitivity;

        // Clamp the X rotation
        if (rotX < -90)
        {
            rotX = -90;
        }
        else if (rotX > 90)
        {
            rotX = 90;
        }

        transform.rotation = Quaternion.Euler(0, rotY, 0); // Rotates the collider
        playerView.rotation = Quaternion.Euler(rotX, rotY, 0); // Rotates the camera

        /* Movement, here's the important part */
        QueueJump();

        if (controller.isGrounded)
        {
            GroundMove();
        }
        else
        {
            AirMove();
        }

        // Move the controller
        controller.Move(playerVelocity * Time.deltaTime);

        /* Calculate top velocity */
        var udp = playerVelocity;
        udp.y = 0.0f;

        if (playerVelocity.magnitude > playerTopVelocity)
        {
            playerTopVelocity = playerVelocity.magnitude;
        }
    }


    /*******************************************************************************************************\
    |* MOVEMENT
    \*******************************************************************************************************/

    /// <summary>
    /// Sets the movement direction based on player input
    /// </summary>
    private void SetMovementDir()
    {
        cmd.forwardmove = Input.GetAxis("Vertical");
        cmd.rightmove = Input.GetAxis("Horizontal");
    }

    /// <summary>
    /// Queues the next jump just like in Q3
    /// </summary>
    private void QueueJump()
    {
        if (Input.GetKeyDown(KeyCode.Space) && !wishJump)
        {
            wishJump = true;
        }
        if (Input.GetKeyUp(KeyCode.Space))
        {
            wishJump = false;
        }
    }

    /// <summary>
    /// Execs when the player is in the air
    /// </summary>
    private void AirMove()
    {
        Vector3 wishdir;
        float accel;

        var scale = CmdScale();

        SetMovementDir();

        wishdir = new Vector3(cmd.rightmove, 0, cmd.forwardmove);
        wishdir = transform.TransformDirection(wishdir);

        var wishspeed = wishdir.magnitude;
        wishspeed *= moveSpeed;

        wishdir.Normalize();
        wishspeed *= scale;

        // CPM: Aircontrol
        var wishspeed2 = wishspeed;
        if (Vector3.Dot(playerVelocity, wishdir) < 0)
        {
            accel = airDeacceleration;
        }
        else
        {
            accel = airAcceleration;
        }

        // If the player is ONLY strafing left or right
        if (cmd.forwardmove == 0 && cmd.rightmove != 0)
        {
            if (wishspeed > sideStrafeSpeed)
            {
                wishspeed = sideStrafeSpeed;
            }
            accel = sideStrafeAcceleration;
        }

        Accelerate(wishdir, wishspeed, accel);

        if (airControl > 0)
        {
            AirControl(wishdir, wishspeed2);
        }
        // !CPM: Aircontrol

        // Apply gravity
        playerVelocity.y -= gravity * Time.deltaTime;

        // LEGACY MOVEMENT SEE BOTTOM
    }

    /// <summary>
    /// * Air control occurs when the player is in the air, it allows
    /// players to move side to side much faster rather than being
    /// 'sluggish' when it comes to cornering.
    /// </summary>
    /// <param name="wishdir"></param>
    /// <param name="wishspeed"></param>
    private void AirControl(Vector3 wishdir, float wishspeed)
    {
        float zspeed;
        float speed;
        float dot;
        float k;
        //float i;

        // Can't control movement if not moving forward or backward
        if (cmd.forwardmove == 0 || wishspeed == 0)
        {
            return;
        }

        zspeed = playerVelocity.y;
        playerVelocity.y = 0;
        /* Next two lines are equivalent to idTech's VectorNormalize() */
        speed = playerVelocity.magnitude;
        playerVelocity.Normalize();

        dot = Vector3.Dot(playerVelocity, wishdir);
        k = 32;
        k *= airControl * dot * dot * Time.deltaTime;

        // Change direction while slowing down
        if (dot > 0)
        {
            playerVelocity.x = playerVelocity.x * speed + wishdir.x * k;
            playerVelocity.y = playerVelocity.y * speed + wishdir.y * k;
            playerVelocity.z = playerVelocity.z * speed + wishdir.z * k;

            playerVelocity.Normalize();
        }

        playerVelocity.x *= speed;
        playerVelocity.y = zspeed; // Note this line
        playerVelocity.z *= speed;

    }

    /// <summary>
    /// Called every frame when the engine detects that the player is on the ground
    /// </summary>
    private void GroundMove()
    {
        Vector3 wishdir;
        //Vector3 wishvel;

        // Do not apply friction if the player is queueing up the next jump
        if (!wishJump)
        {
            ApplyFriction(1.0f);
        }
        else
        {
            ApplyFriction(0f);
        }

        var scale = CmdScale();

        SetMovementDir();

        wishdir = new Vector3(cmd.rightmove, 0, cmd.forwardmove);
        wishdir = transform.TransformDirection(wishdir);
        wishdir.Normalize();

        var wishspeed = wishdir.magnitude;
        wishspeed *= moveSpeed;

        Accelerate(wishdir, wishspeed, runAcceleration);

        // Reset the gravity velocity
        playerVelocity.y = 0;

        if (wishJump)
        {
            playerVelocity.y = jumpSpeed;
            wishJump = false;
            PlayJumpSound();
        }
    }

    /// <summary>
    /// Applies friction to the player, called in both the air and on the ground
    /// </summary>
    /// <param name="t"></param>
    private void ApplyFriction(float t)
    {
        Vector3 vec = playerVelocity; // Equivalent to: VectorCopy();
        ////float vel;
        float speed;
        float newspeed;
        float control;
        float drop;

        vec.y = 0.0f;
        speed = vec.magnitude;
        drop = 0.0f;

        /* Only if the player is on the ground then apply friction */
        if (controller.isGrounded)
        {
            control = speed < runDeacceleration ? runDeacceleration : speed;
            drop = control * friction * Time.deltaTime * t;
        }

        newspeed = speed - drop;

        if (newspeed < 0)
        {
            newspeed = 0;
        }
        if (speed > 0)
        {
            newspeed /= speed;
        }

        playerVelocity.x *= newspeed;
        // playerVelocity.y *= newspeed;
        playerVelocity.z *= newspeed;
    }

    /// <summary>
    /// Calculates wish acceleration based on player's cmd wishes
    /// </summary>
    private void Accelerate(Vector3 wishdir, float wishspeed, float accel)
    {
        float addspeed;
        float accelspeed;
        float currentspeed;

        currentspeed = Vector3.Dot(playerVelocity, wishdir);
        addspeed = wishspeed - currentspeed;

        if (addspeed <= 0)
        {
            return;
        }

        accelspeed = accel * Time.deltaTime * wishspeed;

        if (accelspeed > addspeed)
        {
            accelspeed = addspeed;
        }

        playerVelocity.x += accelspeed * wishdir.x;
        playerVelocity.z += accelspeed * wishdir.z;
    }

    private void LateUpdate()
    {

    }

    /// <summary>
    /// PM_CmdScale
    /// Returns the scale factor to apply to cmd movements
    /// This allows the clients to use axial -127 to 127 values for all directions
    /// without getting a sqrt(2) distortion in speed.
    /// </summary>
    /// <returns></returns>
    private float CmdScale()
    {
        int max;
        float total;
        float scale;

        max = (int)Mathf.Abs(cmd.forwardmove);

        if (Mathf.Abs(cmd.rightmove) > max)
        {
            max = (int)Mathf.Abs(cmd.rightmove);
        }
        if (max == 0)
        {
            return 0;
        }

        total = Mathf.Sqrt(cmd.forwardmove * cmd.forwardmove + cmd.rightmove * cmd.rightmove);
        scale = moveSpeed * max / (moveScale * total);

        return scale;
    }

    /// <summary>
    /// Plays a random jump sound
    /// </summary>
    private void PlayJumpSound()
    {
        // Don't play a new sound while the last hasn't finished
        var source = GetComponent<AudioSource>();

        if (source.isPlaying)
        {
            return;
        }

        var jumpSounds = jumpSoundsProvider.GetJumpSounds();

        source.clip = jumpSounds[Random.Range(0, jumpSounds.Length)];
        source.Play();
    }
}
