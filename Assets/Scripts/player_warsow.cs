using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class player_warsow : MonoBehaviour {

    private PlayerData player_data;
    // Use this for initialization
    void Start () {
		
	}
	
	// Update is called once per frame
	void FixedUpdate()
    {
        PM_CheckJump();
        //PM_CheckDash();
        //PM_CheckWallJump();
        //PM_CheckCrouchSlide();
        //PM_Friction();
    }

    void PM_CheckJump()
    {
        if (player_data.upPush < 10)
        {
            // not holding jump

            //ME: If player was holding jump and auto-jump was activated then we need to clear the flag
            //if (!(pm->playerState->pmove.stats[PM_STAT_FEATURES] & PMFEAT_CONTINOUSJUMP))
            //{
            //    pm->playerState->pmove.pm_flags &= ~PMF_JUMP_HELD;
            //}

            // Since we are not holding jump we are done here
            return;
        }

        //ME: From now we know player is jumping

        //ME: If Autojump was not activated and jump was already pressed ???
        //if (!(pm->playerState->pmove.stats[PM_STAT_FEATURES] & PMFEAT_CONTINOUSJUMP))
        //{
        //    // must wait for jump to be released
        //    if (pm->playerState->pmove.pm_flags & PMF_JUMP_HELD)
        //    {
        //        return;
        //    }
        //}

        //ME:If player is not on ground he cannot jump
        //if (pm->groundentity == -1)
        //{
        //    return;
        //}

        //ME:If jump is not enabled do nothing ???
        //if (!(pm->playerState->pmove.stats[PM_STAT_FEATURES] & PMFEAT_JUMP))
        //{
        //    return;
        //}

        //ME:If auto-jump enable set flag
        //if (!(pm->playerState->pmove.stats[PM_STAT_FEATURES] & PMFEAT_CONTINOUSJUMP))
        //{
        //    pm->playerState->pmove.pm_flags |= PMF_JUMP_HELD;
        //}

        //ME: Since player will be jumping he cannot touch ground;
        //pm->groundentity = -1;

        // clip against the ground when jumping if moving that direction
        if (pml.groundplane.normal[2] > 0 && pml.velocity[2] < 0 && DotProduct2D(pml.groundplane.normal, pml.velocity) > 0)
        {
            GS_ClipVelocity(pml.velocity, pml.groundplane.normal, pml.velocity, PM_OVERBOUNCE);
        }

        pm->playerState->pmove.skim_time = PM_SKIM_TIME;

        //if( gs.module == GS_MODULE_GAME ) GS_Printf( "upvel %f\n", pml.velocity[2] );
        if (pml.velocity[2] > 100)
        {
            module_PredictedEvent(pm->playerState->POVnum, EV_DOUBLEJUMP, 0);
            pml.velocity[2] += pml.jumpPlayerSpeed;
        }
        else if (pml.velocity[2] > 0)
        {
            module_PredictedEvent(pm->playerState->POVnum, EV_JUMP, 0);
            pml.velocity[2] += pml.jumpPlayerSpeed;
        }
        else
        {
            module_PredictedEvent(pm->playerState->POVnum, EV_JUMP, 0);
            pml.velocity[2] = pml.jumpPlayerSpeed;
        }

        // remove wj count
        pm->playerState->pmove.pm_flags &= ~PMF_JUMPPAD_TIME;
        PM_ClearDash();
        PM_ClearWallJump();
    }

    public static class Constant
    {
        const float SPEEDKEY = 500;

        const float PM_DASHJUMP_TIMEDELAY = 1300; // delay in milliseconds
        const float PM_WALLJUMP_TIMEDELAY = 1300;
        const float PM_WALLJUMP_FAILED_TIMEDELAY = 700;
        const float PM_SPECIAL_CROUCH_INHIBIT = 400;
        const float PM_AIRCONTROL_BOUNCE_DELAY = 200;
        const float PM_OVERBOUNCE = 1.01f;
        const float PM_CROUCHSLIDE = 1500;
        const float PM_CROUCHSLIDE_FADE = 500;
        const float PM_CROUCHSLIDE_TIMEDELAY = 700;
        const float PM_CROUCHSLIDE_CONTROL = 3;
        const float PM_FORWARD_ACCEL_TIMEDELAY = 0; // delay before the forward acceleration kicks in
        const float PM_SKIM_TIME = 230;
    }
}


public class PlayerData
{
    public float upPush; // Y+ "force" up
    public float forwardPush; // Z+ "force" forward
    public float sidePush; //x+ "force" forward

    public truc playerState;
}