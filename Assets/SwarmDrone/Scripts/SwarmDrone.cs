﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ExtensionMethods;
using static UnityEngine.Mathf;
using Random = UnityEngine.Random;

public class SwarmDrone : MonoBehaviour {
    #region Variables

    public bool isControlledBySwarm = true;
    public float updateFrequency = 10; //e.g. 10 times / second
    public float proxMult, alignMult, goalMult;
    public float scanRadius = 5f; //How far each bot scans for other bots
    public float swarmSpread = 1f; //How far each robot should stay from other robots
    public float swarmSpreadTolerance = .3f; //How much wiggle room they get
    public float repulsionForce = 0.02f; //How strongly they should repel away
    public float fwdControl, turnControl;
    public Vector3 botMoveVector; //used if isControlledBySwarm
    public GameObject head;
    public LayerMask layerMask; //what should we ignore for collision detection rays
    public List<WheelCollider> wheelsR, wheelsL;
    public MeshRenderer headTexture;
    public float speed = 50f;
    public float turnSpeed = 5f;
    public float turnSharpness = 0.3f; //defines how sharp turns should be when changing botMoveVector
    private Rigidbody _rb;
    private Transform _target;

    #endregion

    private void Start() {
        _rb = GetComponent<Rigidbody>();
        _target = GameObject.FindGameObjectWithTag("GoalBall").transform;
        headTexture.material.color = Color.HSVToRGB(Random.Range(0f, 1f), Random.Range(.2f, 1f), Random.Range(.5f, 1f));

        InvokeRepeating(nameof(FlockingControl), 0, 1/updateFrequency);
    }

    private void FixedUpdate() {
        if(isControlledBySwarm)
            SetMoveByVector(botMoveVector);
        Move(fwdControl, turnControl);
    }

    /// <summary>
    ///     Sets bot movement based on flocking algorithm.
    /// </summary>
    private void FlockingControl() {
        Vector3 flockingControlVector = proxMult * CalcProximalControl()
                                        + alignMult * CalcAlignControl()
                                        + goalMult * CalcGoalControl();

        flockingControlVector = Vector3.ClampMagnitude(flockingControlVector, 1);
        if(isControlledBySwarm)
            botMoveVector = flockingControlVector;
    }

    private Vector3 CalcProximalControl() {
        Vector3 proxVec = Vector3.zero;

        //select headColliders within scanRadius of this bot
        foreach(Collider headCollider in Physics.OverlapSphere(transform.position, scanRadius, 1 << 11)) {
            Transform botT = headCollider.transform.parent;
            if(botT.gameObject.Equals(gameObject))
                continue; //Skip this bot

            float di = Vector3.Distance(transform.position, botT.position);
            if(Abs(di - swarmSpread) < swarmSpreadTolerance) {
                //They are close enough, so slow them downn relative to each other
                proxVec += (headCollider.attachedRigidbody.velocity - _rb.velocity).normalized;
            }

            float sigma = swarmSpread / Pow(2, 1 / 6f);
            float eps = repulsionForce;
            float pidi = -8 * eps * (2 * Pow(sigma, 4) / Pow(di, 5) - Pow(sigma, 2) / Pow(di, 3));
            Vector3 phi = (botT.position - transform.position).normalized;

            proxVec += pidi * phi;
        }

        return proxVec;
    }

    private Vector3 CalcAlignControl() {
        Vector3 alignVec = Vector3.zero;
        return alignVec;
    }

    private Vector3 CalcGoalControl() {
        Vector3 goalVec = Vector3.zero;

        RaycastHit hit;
//        Vector3 p1 = transform.position + 0.3f * transform.right;
//        Vector3 p2 = transform.position - 0.3f * transform.right;
//        Debug.DrawRay(head.transform.position, transform.forward * (_rb.velocity.magnitude * 0.3f + 0.5f), Color.red);
//        Debug.DrawRay(head.transform.position, (transform.forward + transform.right * .5f) * (_rb.velocity.magnitude * 0.3f + 0.5f), Color.red);
//        Debug.DrawRay(head.transform.position, (transform.forward - transform.right * .5f) * (_rb.velocity.magnitude * 0.3f + 0.5f), Color.red);
        if(Physics.Raycast(head.transform.position, transform.forward, out hit, _rb.velocity.magnitude * 0.3f + 0.5f, layerMask)) {
            Vector3 diff = transform.position - hit.point;
            goalVec += diff.normalized * Lerp(5f, .1f, diff.sqrMagnitude) + Random.Range(-.3f, .3f) * transform.right;
        } else if(Physics.Raycast(head.transform.position, transform.forward + transform.right * .5f, out hit, _rb.velocity.magnitude * 0.3f + 0.5f, layerMask)) {
            Vector3 diff = transform.position - hit.point;
            goalVec += diff.normalized * Lerp(5f, .1f, diff.sqrMagnitude) - 0.5f * transform.right;
        } else if(Physics.Raycast(head.transform.position, transform.forward - transform.right * .5f, out hit, _rb.velocity.magnitude * 0.3f + 0.5f, layerMask)) {
            Vector3 diff = transform.position - hit.point;
            goalVec += diff.normalized * Lerp(5f, .1f, diff.sqrMagnitude) + 0.5f * transform.right;
        }else if(_target != null) {
            goalVec += Vector3.ClampMagnitude(_target.position - transform.position, 1);
        }

        return goalVec;
    }

    /// <summary>
    ///     Sets bot movement based on a vector, where vector magnitude controls bot speed.
    /// </summary>
    /// <param name="moveVector">Vector that points where the bot should go, with magnitude between 0 and 1</param>
    private void SetMoveByVector(Vector3 moveVector) {
        Debug.DrawRay(transform.position, moveVector / 2f, Color.green);

        //Slows us down for turns
        float fwdSpeedControl = Vector3.Dot(transform.forward, moveVector.normalized);
        //Controls the amount we slow down per turn
        fwdSpeedControl = Lerp(moveVector.magnitude, moveVector.magnitude * fwdSpeedControl, turnSharpness);
        //Prevents stopping too fast
        fwdControl = Extensions.SharpInDamp(fwdControl, fwdSpeedControl, 1f);

        //Turns faster the larger the angle is, slows down as we approach desired heading
        float turnSpeedControl = Deg2Rad * Vector3.Angle(transform.forward, moveVector);
        //Slows down turning if trying to turn too fast while moving too fast so we don't flip
        turnSpeedControl *= Lerp(1f, .5f, Lerp(0, fwdControl, turnSpeedControl / 3));

        //Decides which direction we should be turning
        float turnDirectionControl = Vector3.Dot(transform.right, moveVector.normalized);
        turnDirectionControl = turnDirectionControl / Abs(turnDirectionControl + 0.01f);

        turnControl = turnDirectionControl * turnSpeedControl;
    }

    /// <summary>
    ///     Controls the bot movement. Should only be called once per physics timestep (last call will always override).
    /// </summary>
    /// <param name="fwdInput">Float between -1 and 1 that controls bots forward movement</param>
    /// <param name="turnInput">Float between -1 and 1 specifying how fast to turn. 1 is full right, -1 full left.</param>
    private void Move(float fwdInput, float turnInput) {
        foreach(WheelCollider wheel in wheelsR)
            wheel.motorTorque = fwdInput * speed - turnInput * turnSpeed;
        foreach(WheelCollider wheel in wheelsL)
            wheel.motorTorque = fwdInput * speed + turnInput * turnSpeed;
    }
}