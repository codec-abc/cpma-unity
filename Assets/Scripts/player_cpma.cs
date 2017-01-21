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

    Transform playerView;  // Must be a camera
    float playerViewYOffset = 0.6f; // The height at which the camera is bound to
    float xMouseSensitivity = 30.0f;
    float yMouseSensitivity = 30.0f;

    /* Frame occuring factors */
    float gravity  = 20.0f;
    float friction = 6f;                // Ground friction

    /* Movement stuff */
    float moveSpeed              = 7.0f;  // Ground move speed
    float runAcceleration        = 14f;   // Ground accel
    float runDeacceleration      = 10f;   // Deacceleration that occurs when running on the ground
    float airAcceleration        = 2.0f;  // Air accel
    float airDeacceleration      = 2.0f;    // Deacceleration experienced when opposite strafing
    float airControl             = 0.3f;  // How precise air control is
    float sideStrafeAcceleration = 50f;   // How fast acceleration occurs to get up to sideStrafeSpeed when side strafing
    float sideStrafeSpeed        = 1f;    // What the max speed to generate when side strafing
    float jumpSpeed              = 8.0f;  // The speed at which the character's up axis gains when hitting jump
    float moveScale              = 1.0f;

    /* print() styles */
    GUIStyle style;

    /* Sound stuff */
    AudioClip[] jumpSounds;

    /* FPS Stuff */
    float fpsDisplayRate = 4.0f;  // 4 updates per sec.

    private int frameCount = 0;
    private float dt = 0.0f;
    private float fps = 0.0f;

    private CharacterController controller;

    // Camera rotationals
    private float rotX = 0.0f;
    private float rotY = 0.0f;

    private Vector3 moveDirection = Vector3.zero;
    private Vector3 moveDirectionNorm = Vector3.zero;
    private Vector3 playerVelocity= Vector3.zero;
    private float playerTopVelocity = 0.0f;

    // If true then the player is fully on the ground
    private bool grounded = false;

    // Q3: players can queue the next jump just before he hits the ground
    private bool wishJump = false;

    // Used to display real time friction values
    private float playerFriction = 0.0f;

    // Contains the command the user wishes upon the character
    class Cmd
    {
        public float forwardmove;
	    public float rightmove;
	    public float upmove;
    }

    private Cmd cmd; // Player commands, stores wish commands that the player asks for (Forward, back, jump, etc)

    private Vector3 playerSpawnPos ;
    private Quaternion playerSpawnRot ;

    private void Start()
    {
        /* Hide the cursor */
        //Screen.showCursor = false;
        Cursor.visible = false;

        //Screen.lockCursor = true;
        Cursor.lockState = CursorLockMode.Locked;

        /* Put the camera inside the capsule collider */
        //playerView.position = this.transform.position;
        playerView.position = new Vector3 (transform.position.x, transform.position.y + playerViewYOffset, transform.position.z);

        controller = GetComponent<CharacterController>();// GetComponent(CharacterController);
        cmd = new Cmd();

        // Set the spawn position of the player
        playerSpawnPos = transform.position;
        playerSpawnRot = playerView.rotation;
    }

    private void Update()
    {
        /* Do FPS calculation */
        frameCount++;
        dt += Time.deltaTime;

        if (dt > 1.0f / fpsDisplayRate)
        {
            fps = Mathf.Round(frameCount / dt);
            frameCount = 0;
            dt -= 1.0f / fpsDisplayRate;
        }

        /* Ensure that the cursor is locked into the screen */
        //if (Screen.lockCursor == false)
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (Input.GetMouseButtonDown(0))
            {
                //Screen.lockCursor = true;
                Cursor.lockState = CursorLockMode.Locked;
            }
        }

        /* Camera rotation stuff, mouse controls this shit */
        rotX -= Input.GetAxis("Mouse Y") * xMouseSensitivity * 0.02f;
        rotY += Input.GetAxis("Mouse X") * yMouseSensitivity * 0.02f;

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

        // Set the camera's position to the transform
        //playerView.position = transform.position;
        //playerView.position.y = transform.position.y + playerViewYOffset;

        playerView.position = new Vector3 (transform.position.x, transform.position.y + playerViewYOffset, transform.position.z);

        /* Movement, here's the important part */
        QueueJump();

        if (controller.isGrounded)
        {
            GroundMove();
        }
        else if (!controller.isGrounded)
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

    /**
     * Sets the movement direction based on player input
     */
    private void SetMovementDir()
    {
        cmd.forwardmove = Input.GetAxis("Vertical");
        cmd.rightmove = Input.GetAxis("Horizontal");
    }

    /**
     * Queues the next jump just like in Q3
     */
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

    /**
     * Execs when the player is in the air
     */
    private void AirMove()
    {
        Vector3 wishdir;
        float wishvel = airAcceleration;
        float accel;

        var scale = CmdScale();

        SetMovementDir();

        wishdir = new Vector3(cmd.rightmove, 0, cmd.forwardmove);
        wishdir = transform.TransformDirection(wishdir);

        var wishspeed = wishdir.magnitude;
        wishspeed *= moveSpeed;

        wishdir.Normalize();
        moveDirectionNorm = wishdir;
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

    /**
     * Air control occurs when the player is in the air, it allows
     * players to move side to side much faster rather than being
     * 'sluggish' when it comes to cornering.
     */
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
            moveDirectionNorm = playerVelocity;
        }

        playerVelocity.x *= speed;
        playerVelocity.y = zspeed; // Note this line
        playerVelocity.z *= speed;

    }

    /**
     * Called every frame when the engine detects that the player is on the ground
     */
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
        moveDirectionNorm = wishdir;

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

    /**
     * Applies friction to the player, called in both the air and on the ground
     */
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
        playerFriction = newspeed;

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

    /**
     * Calculates wish acceleration based on player's cmd wishes
     */
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

    private void OnGUI()
    {
        GUI.Label( new Rect(0, 0, 400, 100), "FPS: " + fps, style);
        var ups = controller.velocity;
        ups.y = 0;
        GUI.Label(new Rect(0, 15, 400, 100), "Speed: " + Mathf.Round(ups.magnitude * 100) / 100 + "ups", style);
        GUI.Label(new Rect(0, 30, 400, 100), "Top Speed: " + Mathf.Round(playerTopVelocity * 100) / 100 + "ups", style);
    }

    /*
    ============
    PM_CmdScale

    Returns the scale factor to apply to cmd movements
    This allows the clients to use axial -127 to 127 values for all directions
    without getting a sqrt(2) distortion in speed.
    ============
    */
    private float CmdScale()
    {
        int max;
        float total;
        float scale;

        max = (int) Mathf.Abs(cmd.forwardmove);

        if (Mathf.Abs(cmd.rightmove) > max)
        {
            max = (int)Mathf.Abs(cmd.rightmove);
        }
        if (max != 0)
        {
            return 0;
        }

        total = Mathf.Sqrt(cmd.forwardmove * cmd.forwardmove + cmd.rightmove * cmd.rightmove);
        scale = moveSpeed * max / (moveScale * total);

        return scale;
    }


    /**
     * Plays a random jump sound
     */
    private void PlayJumpSound()
    {
        // Don't play a new sound while the last hasn't finished
        var source = GetComponent<AudioSource>();

        //if (audio.isPlaying)
        //{
        //    return;
        //}

        if (source.isPlaying)
        {
            return;
        }

        //audio.clip = jumpSounds[Random.Range(0, jumpSounds.length)];
        //audio.Play();

        source.clip = jumpSounds[Random.Range(0, jumpSounds.Length)];
        source.Play();
    }

    //private void PlayerExplode()
    //{
    //    var velocity = controller.velocity;
    //    velocity.Normalize();
    //    var gibEffect = Instantiate(gibEffectPrefab, transform.position, Quaternion.identity);
    //    gibEffect.GetComponent(GibFX).Explode(transform.position, velocity, controller.velocity.magnitude);
    //    isDead = true;
    //}

    //private void PlayerSpawn()
    //{
    //    this.transform.position = playerSpawnPos;
    //    this.playerView.rotation = playerSpawnRot;
    //    rotX = 0.0;
    //    rotY = 0.0;
    //    playerVelocity = Vector3.zero;
    //    isDead = false;
    //}


    // Legacy movement

    // var wishdir : Vector3;
    // var wishvel : float = airAcceleration;

    // // var scale = CmdScale();

    // SetMovementDir();

    // /* If the player is just strafing in the air 
    //    this simulates CPM (Not very accurately by
    //    itself) */
    // if(cmd.forwardmove == 0 && cmd.rightmove != 0)
    // {
    // 	wishvel = airStrafeAcceleration;
    // }

    // wishdir = Vector3(cmd.rightmove, 0, cmd.forwardmove);
    // wishdir = transform.TransformDirection(wishdir);
    // wishdir.Normalize();
    // moveDirectionNorm = wishdir;

    // var wishspeed = wishdir.magnitude;
    // wishspeed *= moveSpeed;

    // Accelerate(wishdir, wishspeed, wishvel);

    // // Apply gravity
    // playerVelocity.y -= gravity * Time.deltaTime;
}
