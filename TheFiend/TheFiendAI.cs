using GameNetcodeStuff;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.PlayerLoop;

public class TheFiendAI : EnemyAI
{
    public NetworkVariable<int> StateOfMind,Funky = new NetworkVariable<int>(1,default,default);

    private Animator animator;
    public GameObject Main, Neck, Spine, LeftHand, RightHand;
    public MeshRenderer MapDot;
    public SkinnedMeshRenderer skinnedMesh;
    private float OldYScale;
    public System.Random enemyRandom;
    public AudioClip[] audioClips;
    public AudioClip StepClip;
    private AudioSource AS, AS2;
    private Vector3 FavSpot;
    private bool ResetNode, EatingPlayer;
    public NetworkVariable<bool> Seeking, Invis, RageMode, GlobalCD, StandingMode = new NetworkVariable<bool>(false,default,default);
    public NetworkVariable<bool> LungApparatusWillRage = new NetworkVariable<bool>(TheFiend.TheFiend.WillRageAfterApparatus.Value, default, default);
    public Quaternion OldR;
    public bool Step;
    private Vector3 LastPos;
    private Vector3 Node;
    private int LightTriggerTimes;

    private NavMeshPath path;
    private GameObject Head, breakerBox;
    private RoundManager roundManager;
    public TimeOfDay timeOfDay;

    private GameObject LungApparatus; 
    private Vector3 LungApparatusPosition;

    public ParticleSystem PS;
    public GameObject TargetLook;



    public void Awake()
    {
        path = new NavMeshPath();
        FavSpot = transform.position;
        Head = Neck.transform.Find("mixamorig:Head").gameObject;
        OldR = Neck.transform.localRotation;
        animator = GetComponent<Animator>();
        animator.Play("Idle");
        AS = GetComponent<AudioSource>();
        AS2 = Spine.GetComponent<AudioSource>();
        try
        {
            breakerBox = FindObjectOfType<BreakerBox>().gameObject;
        }
        catch
        {
            breakerBox = null;
        }
        roundManager = FindObjectOfType<RoundManager>();
        timeOfDay = FindObjectOfType<TimeOfDay>();
        MapDot.material.color = Color.red;
        LungApparatus = GameObject.Find("LungApparatus(Clone)");
        if (LungApparatus)
            LungApparatusPosition = LungApparatus.transform.position;

        var mixer = SoundManager.Instance.diageticMixer.FindMatchingGroups("SFX")[0];
        AS.outputAudioMixerGroup = mixer;
    }
    public override void Start()
    {
        base.Start();
        OldYScale = Main.transform.position.y;
        enemyRandom = new System.Random(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
        AS.clip = audioClips[0];
        AS.loop = true;
        AS.Play();
        if (roundManager.currentLevel.currentWeather == LevelWeatherType.Eclipsed)
        {
            PS.Play();
            PS.gameObject.GetComponent<AudioSource>().Play();
            Funky.Value = 3;
        }
    }
    public void FixedUpdate()
    {
        if (Step == true && Invis.Value == false)
        {
            AS2.pitch = Random.Range(0.6f, 1f);
            AS2.PlayOneShot(StepClip);
        }
    }
    public void LateUpdate()
    {
        if (TargetLook != null)
        {
            Neck.transform.LookAt(TargetLook.transform, Vector3.up);
        }
        else
        {
            Neck.transform.localRotation = OldR;
        }
    }
    public override void DoAIInterval()
    {
        base.DoAIInterval();
        if (timeOfDay.hour >= 15)
            Funky.Value = 2;
        if (Seeking.Value == true)
            AS.volume = 0;
        else
            if (StateOfMind.Value != 3)
                AS.volume = TheFiend.TheFiend.Volume.Value;
        if (Invis.Value)
            skinnedMesh.enabled = false;
        else
            skinnedMesh.enabled = true;
        if (Random.Range(1, 10000) == 1)
        {
            TeleportServerRpc();
        }
        if (Random.Range(1, 10000 / Funky.Value) == 1)
        {
           if (StateOfMind.Value == 3)
           {
              HideOnCellingServerRpc();
           }
        }
        if (Random.Range(1, 10000 / Funky.Value) == 1 && !Seeking.Value)
        {
            ToggleSeekingServerRpc();
        }
        if (!GlobalCD.Value)
        {
            if (StateOfMind.Value < 3)
            {
                StateOfMind.Value = 0;
            }
            if (breakerBox && !Seeking.Value)
            {
                var bbmesh = breakerBox.transform.Find("Mesh").gameObject;
                if (Vector3.Distance(Main.transform.position, bbmesh.transform.position) <= 5.0f)
                {
                    RaycastHit hitinfo;
                    bool hit = Physics.Raycast(Neck.transform.position, bbmesh.transform.position - Neck.transform.position, out hitinfo, Mathf.Infinity, ~LayerMask.GetMask("Enmies"));
                    if (hit && Vector3.Distance(hitinfo.point, bbmesh.transform.position) < 2.0f)
                    {
                        StateOfMind.Value = 4;
                        TargetLook = bbmesh;
                        if (Vector3.Distance(Main.transform.position, bbmesh.transform.position) <= 2.0f)
                        {
                            BreakerBoxBreakServerRpc();
                        }
                    }
                    else
                    {
                        StateOfMind.Value = 0;
                    }
                }
            }
            if (TargetClosestPlayer(100))
            {
                TargetLook = targetPlayer.gameObject;
                if (targetPlayer.currentlyGrabbingObject)
                {
                    if (targetPlayer.currentlyGrabbingObject.gameObject.name.Contains("FlashlightItem"))
                    {
                        GameObject LightO = targetPlayer.currentlyGrabbingObject.gameObject.transform.Find("Light").gameObject;
                        Light light = LightO.GetComponent<Light>();
                        if (light.enabled == true)
                        {
                            if (Vector3.Distance(Head.transform.position, LightO.transform.position) <= 2.5f)
                            {
                                FearedServerRpc(false,true);
                                LightTriggerTimes += 1;
                            }
                        }
                    }
                }
            }
            if (StateOfMind.Value == 3 && !GlobalCD.Value && !Seeking.Value)
            {
                if (TargetClosestPlayer(100))
                {
                    TargetLook = targetPlayer.gameObject;
                    if (Vector3.Distance(transform.position, targetPlayer.gameObject.transform.position) <= 4)
                    {
                        HideOnCellingServerRpc();
                    }
                }
            }
            if (!EatingPlayer && StateOfMind.Value <= 2 && !GlobalCD.Value && StandingMode.Value == false)
            {
                if (TargetClosestPlayer(100))
                {
                    TargetLook = targetPlayer.gameObject;
                    ResetNode = true;
                    if (agent.remainingDistance > 10f && RageMode.Value == false)
                    {
                        OldYScale = Main.transform.position.y;
                        if (!Seeking.Value)
                        {
                            StateOfMind.Value = 1;
                            agent.speed = 3 + (Funky.Value - 1);
                            animator.Play("Walk");

                            if (CheckDoor())
                            {
                                if (Random.Range(1, 100) == 1)
                                {
                                    if (StateOfMind.Value != 3)
                                    {
                                        HideOnCellingServerRpc();
                                    }
                                }
                            }
                            else
                            {
                                if (Random.Range(1, 1000) == 1)
                                {
                                    if (StateOfMind.Value != 3)
                                    {
                                        HideOnCellingServerRpc();
                                    }
                                }
                            }
                        }
                        else
                        {
                            agent.speed = 1;
                            animator.Play("Seeking");
                            BreakDoorServerRpc();
                        }
                        
                        if (Random.Range(1, TheFiend.TheFiend.FlickerRngChance.Value) == 1)
                        {
                            roundManager.FlickerLights(true, true);
                        }
                    }
                    else
                    {
                        if (!Seeking.Value)
                        {
                            StateOfMind.Value = 2;
                            if (CheckLineOfSightForPlayer() != null)
                            {
                                targetPlayer.JumpToFearLevel(0.9f, true);
                            }
                            if (!RageMode.Value)
                                agent.speed = 9 * Funky.Value;
                            else
                                agent.speed = 20 * Funky.Value;
                            animator.Play("Run");
                            BreakDoorServerRpc();
                        }
                    }
                    SetDestinationToPosition(targetPlayer.transform.position);
                    if (Seeking.Value)
                    {
                        foreach (PlayerControllerB player in FindObjectsOfType(typeof(PlayerControllerB)) as PlayerControllerB[])
                            if (player.HasLineOfSightToPosition(Neck.transform.position))
                            {
                                ToggleSeekingServerRpc();
                                FearedServerRpc(true);
                                break;
                            }
                    }
                }
                else
                {
                    agent.speed = 3;
                    if (ResetNode == true)
                    {
                        WonderVectorServerRpc(60);
                        ResetNode = false;
                    }
                    if (Node != Vector3.zero)
                        SetDestinationToPosition((Vector3)Node);
                    else
                        ResetNode = true;
                    if (agent.remainingDistance == 0f)
                        ResetNode = true;
                    if (Random.Range(1, 100) == 1)
                        ResetNode = true;
                    TargetLook = null;
                }
                if (!GlobalCD.Value)
                {
                    if (agent.remainingDistance == 0f && StateOfMind.Value == 0 && RageMode.Value==false)
                        animator.Play("Idle");
                    else
                    {
                        if (!Seeking.Value && StateOfMind.Value == 1)
                        {
                            animator.Play("Walk");
                        }
                    }
                }
                    
            }
            if (StateOfMind.Value == 4 && TargetLook)
            {
                animator.Play("Walk");
                SetDestinationToPosition(TargetLook.transform.position);
            }
            if (LungApparatus != null && LungApparatusWillRage.Value==true && Invis.Value==false)
            {
                if (LungApparatus.transform.position != LungApparatusPosition)
                {
                    LungApparatus.transform.Find("Point Light").gameObject.GetComponent<Light>().color = Color.red;
                    LungApparatus.GetComponent<LungProp>().scrapValue = 300;
                    LungApparatus = null;
                    StartCoroutine(Rage());
                }
            }
        }
        SyncPositionToClients();
    }
    [ServerRpc]
    public void ToggleSeekingServerRpc()
    {
        Seeking.Value = !Seeking.Value;

    }
    void OnTriggerEnter(Collider collision)
    {
        if (collision.gameObject.GetComponent<PlayerControllerB>() && StateOfMind.Value != 3 && GlobalCD.Value == false && Invis.Value==false)
        {
            if (!EatingPlayer)
            {
                if (Vector3.Distance(transform.position, collision.gameObject.transform.position) < 4f)
                {
                    GrabServerRpc(collision.gameObject.GetComponent<PlayerControllerB>());
                }
            }
        }
    }
    [ServerRpc(RequireOwnership =false)]
    public void SceamServerRpc()
    {
        AS.Stop();
        AS.clip = audioClips[Random.Range(1, 2)];
        AS.loop = false;
        AS.Play();
        SceamClientRpc();
    }
    [ClientRpc]
    public void SceamClientRpc()
    {
        AS.Stop();
        AS.clip = audioClips[Random.Range(1, 2)];
        AS.loop = false;
        AS.Play();
    }

    [ServerRpc(RequireOwnership = false)]
    public void IdleSoundServerRpc()
    {
        AS.Stop();
        AS.clip = audioClips[0];
        AS.loop = true;
        AS.Play();
        IdleSoundClientRpc();
    }
    [ClientRpc]
    public void IdleSoundClientRpc()
    {
        AS.Stop();
        AS.clip = audioClips[0];
        AS.loop = true;
        AS.Play();
    }
    [ServerRpc(RequireOwnership =false)]
    public void GrabServerRpc(NetworkBehaviourReference PlayerControllerBRef)
    {
        if (PlayerControllerBRef.TryGet(out PlayerControllerB controllerB))
        {
            GrabClientRpc(controllerB.gameObject);
        }
    }
    [ClientRpc]
    public void GrabClientRpc(NetworkObjectReference networkObject)
    {
        if (networkObject.TryGet(out NetworkObject networkObject1))
        {
            StartCoroutine(Grabbing(networkObject1.gameObject));
        }
    }
    public IEnumerator Grabbing(GameObject Player)
    {
        if (!EatingPlayer && Player)
        {

            EatingPlayer = true;
            TargetLook = null;
            agent.speed = 0;
            SetDestinationToPosition(agent.transform.position);
            PlayerControllerB PCB = Player.GetComponent<PlayerControllerB>();
            float oldspeed = PCB.movementSpeed;
            PCB.movementSpeed = 0;
            animator.Play("Grab");
            transform.LookAt(Player.transform.position, Vector3.up);
            StartCoroutine(RotatePlayerToMe(PCB));
            SceamServerRpc();
            yield return new WaitForSeconds(1.7f);
            PCB.KillPlayer(Main.transform.forward * 15f,true,CauseOfDeath.Suffocation,1);
            if (PCB.IsOwner)
                PCB.movementSpeed = oldspeed;
            yield return new WaitForSeconds(1f);
            IdleSoundServerRpc();
            animator.Play("Idle");
            yield return new WaitForSeconds(3f);
            yield return new WaitForSeconds(2f);
            RageMode.Value = false;
            if (Random.Range(1, 30) == 1)
            {
                HideOnCellingServerRpc();
            }
            EatingPlayer = false;
        }
    }
    IEnumerator RotatePlayerToMe(PlayerControllerB PCB)
    {
        if (PCB)
        {
            Vector3 Position = transform.position - PCB.gameObject.transform.position;
            while (PCB.health != 0)
            {
                PlayerSmoothLookAt(Position,PCB);
                yield return null;
            }
        }
    }


    void PlayerSmoothLookAt(Vector3 newDirection, PlayerControllerB PCB)
    {
        PCB.gameObject.transform.rotation = Quaternion.Lerp(PCB.gameObject.transform.rotation, Quaternion.LookRotation(newDirection), Time.deltaTime * 5);
    }
    [ServerRpc(RequireOwnership =false)]
    public void HideOnCellingServerRpc()
    {
        if (StateOfMind.Value != 3)
        {
            if (StateOfMind.Value <= 3)
            {
                StateOfMind.Value = 3;
                LastPos = Main.transform.position;
                OldYScale = Main.transform.position.y;
                RaycastHit raycastHit;
                Physics.Raycast(Main.transform.position,transform.TransformDirection(Vector3.up), out raycastHit, Mathf.Infinity, ~LayerMask.GetMask("Enmies"));
                animator.Play("Hide");
                AS.Stop();
                agent.speed = 0;
                SetYLevelClientRpc(raycastHit.point.y);
                MapDot.enabled = false;
            }
        }
        else
        {
            if (StandingMode.Value == false)
            {
                StartCoroutine(Stand());
            }
        }
    }
    [ClientRpc]
    public void SetYLevelClientRpc(float y)
    {
        Main.transform.position = new Vector3(Main.transform.position.x, y, Main.transform.position.z);
    }
    public IEnumerator Stand()
    {
        StandingMode.Value = true;
        MapDot.enabled = true;
        var rig = Main.AddComponent<Rigidbody>();
        rig.detectCollisions = false;
        while (Vector3.Distance(Main.transform.position, LastPos) > 1.5f)
        {
            yield return null;
        }
        Destroy(rig);
        animator.Play("UnHide");
        yield return new WaitForSeconds(.2f);
        SetYLevelClientRpc(OldYScale);
        animator.Play("Idle");
        SceamServerRpc();
        yield return new WaitForSeconds(2f);
        IdleSoundServerRpc();
        BreakDoorServerRpc();
        StateOfMind.Value = 1;
        StandingMode.Value = false;
    }
    [ServerRpc]
    public void BreakDoorServerRpc()
    {
        foreach (DoorLock Door in FindObjectsOfType(typeof(DoorLock)) as DoorLock[])
        {
            var ThisDoor = Door.transform.parent.transform.parent.transform.parent.gameObject;
            if (!ThisDoor.GetComponent<Rigidbody>())
            {
                if (Vector3.Distance(transform.position, ThisDoor.transform.position) <= 4f)
                {
                    BashDoorClientRpc(ThisDoor, (targetPlayer.transform.position - transform.position).normalized * 20);
                }
            }
        }
    }
    [ClientRpc]
    public void BashDoorClientRpc(NetworkObjectReference netObjRef, Vector3 Position)
    {
        if (netObjRef.TryGet(out NetworkObject netObj))
        {
            var ThisDoor = netObj.gameObject;
            var rig = ThisDoor.AddComponent<Rigidbody>();
            var newAS = ThisDoor.AddComponent<AudioSource>();
            newAS.spatialBlend = 1;
            newAS.maxDistance = 60;
            newAS.rolloffMode = AudioRolloffMode.Linear;
            newAS.volume = 3;
            StartCoroutine(TurnOffC(rig, .12f));
            rig.AddForce(Position, ForceMode.Impulse);
            newAS.PlayOneShot(audioClips[3]);
        }
    }
    public bool CheckDoor()
    {
        foreach (DoorLock Door in FindObjectsOfType(typeof(DoorLock)) as DoorLock[])
        {
            var ThisDoor = Door.transform.parent.transform.parent.gameObject;
            if (Vector3.Distance(transform.position, ThisDoor.transform.position) <= 4f)
            {
                return true;
            }
        }
        return false;
    }
    IEnumerator TurnOffC(Rigidbody rigidbody,float time)
    {
        rigidbody.detectCollisions = false;
        yield return new WaitForSeconds(time);
        rigidbody.detectCollisions = true;
        Destroy(rigidbody.gameObject, 5);
    }
    [ServerRpc(RequireOwnership =false)]
    public void FearedServerRpc(bool TempRage, bool uselight=false)
    {
        if (!EatingPlayer && StandingMode.Value == false)
        {
            GlobalCD.Value = true;
            FearedClientRpc();
            StartCoroutine(CD(5));
            float TempVale = 3f;
            if (uselight)
                TempVale = LightTriggerTimes * 2;
            if (TempRage)
                StartCoroutine(SetTempRage(TempVale));

        }
    }
    public IEnumerator Rage()
    {
        GlobalCD.Value = true;
        yield return new WaitForSeconds(.2f);
        animator.Play("Rage");
        foreach (PlayerControllerB player in FindObjectsOfType(typeof(PlayerControllerB)) as PlayerControllerB[])
        {
            player.JumpToFearLevel(.9f);
        }
        AS.maxDistance = 500;
        AS.Stop();
        AS.clip = audioClips[5];
        AS.loop = false;
        AS.Play();
        yield return new WaitForSeconds(9f);
        ToggleRageServerRpc(true);
        AS.maxDistance = 30;
        GlobalCD.Value = false;
        yield return new WaitForSeconds(20f);
        ToggleRageServerRpc(false);
    }
    [ServerRpc(RequireOwnership =false)]
    public void ToggleRageServerRpc(bool TheRageValue)
    {
        RageMode.Value = TheRageValue;
    }
    [ServerRpc(RequireOwnership = false)]
    public void TeleportServerRpc()
    {
        if (Invis.Value == false)
        {
            Invis.Value = true;
            List<PlayerControllerB> playerControllerBs = new List<PlayerControllerB>();
            foreach (PlayerControllerB player in FindObjectsOfType(typeof(PlayerControllerB)) as PlayerControllerB[])
            {
                if (player.isInsideFactory)
                {
                    playerControllerBs.Add(player);
                }
            }

            if (playerControllerBs.Count > 0)
            {
                transform.position = playerControllerBs[Random.Range(1, playerControllerBs.Count)].gameObject.transform.position;
            }
            GlobalCD.Value = true;
            StartCoroutine(CD(25, true));
        }
    }
    [ClientRpc]
    public void FearedClientRpc()
    {
        animator.Play("CoverFace");
        agent.speed = 0;
        AS.Stop();
        AS.clip = audioClips[4];
        AS.loop = false;
        AS.Play();
    }
    IEnumerator CD(float time,bool UnInvis=false)
    {
        agent.speed = 0;
        yield return new WaitForSeconds(time);
        GlobalCD.Value = false;
        if (UnInvis)
            Invis.Value = false;
    }
    IEnumerator StateMindCD(float time, int typenow)
    {
        yield return new WaitForSeconds(time);
        StateOfMind.Value = typenow;
    }
    IEnumerator SetTempRage(float time)
    {
        ToggleRageServerRpc(true);
        yield return new WaitForSeconds(time);
        ToggleRageServerRpc(false);
    }
    [ServerRpc(RequireOwnership =false)]
    public void WonderVectorServerRpc(float Range)
    {
        Vector3 vector3 = transform.position + new Vector3(Random.Range(-Range, Range), 0, Random.Range(-Range, Range));
        if (agent.CalculatePath(vector3, path))
            Node=vector3;
        else
            Node= Vector3.zero;
    }
    [ServerRpc]
    public void BreakerBoxBreakServerRpc()
    {
        if (!breakerBox.GetComponent<Rigidbody>())
        {
            BreakerBoxBreakClientRpc(breakerBox);
        }
    }
    [ClientRpc]
    public void BreakerBoxBreakClientRpc(NetworkObjectReference networkObjectReference)
    {
        if (networkObjectReference.TryGet(out NetworkObject networkObject))
        {
            var bbox = networkObject.gameObject;
            bbox.transform.Find("Mesh").transform.Find("PowerBoxDoor").gameObject.AddComponent<Rigidbody>();
            var rig = bbox.AddComponent<Rigidbody>();
            StartCoroutine(TurnOffC(rig, .1f));
            rig.AddForce((Neck.transform.position - transform.position).normalized * 15, ForceMode.Impulse);
            bbox.GetComponent<AudioSource>().PlayOneShot(audioClips[3]);
            Destroy(bbox, 5);
            bbox = null;
            roundManager.PowerSwitchOffClientRpc();
            StartCoroutine(StateMindCD(1, 0));
            animator.Play("Grab");
        }
    }

}
