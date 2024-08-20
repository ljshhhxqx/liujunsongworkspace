using Common;
using Mirror;
using Tool.GameEvent;
using UnityEngine;
using VContainer;

public class PlayerController : NetworkBehaviour
{
    //决定摄像机的旋转中心
    [SerializeField]
    private Transform rotateCenter;

    private CharacterController characterController;
    private PlayerDataConfig playerDataConfig;
    private GameDataConfig gameDataConfig;
    //单端的事件系统，比如客户端-客户端
    private GameEventManager gameEventManager;
    private Rigidbody rb;
    private Animator animator;
    //玩家是否在地面上
    private bool isGrounded;
    //是否完整初始化
    private bool isReady;
    //决定跳跃是否被触发
    private bool isJumpTriggered;
    //用来决定玩家移动方向
    private Vector3 movement;
    //玩家的摄像机
    private Camera camera;

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="configProvider">配置提供器</param>
    /// <param name="gameEventManager"></param>
    [Inject]
    private void Init(IConfigProvider configProvider, GameEventManager gameEventManager)
    {
        this.gameEventManager = gameEventManager;
        //this.gameEventManager.Publish(new PlayerSpawnedEvent(rotateCenter));
        //由配置提供器提供配置数据
        playerDataConfig = configProvider.GetConfig<PlayerDataConfig>();
        gameDataConfig = configProvider.GetConfig<GameDataConfig>();
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        camera = Camera.main;
        isReady = true;
        Debug.Log("OnStartLocalPlayer");
    }

    [ClientCallback]
    public override void OnStartLocalPlayer()
    {
        //在这里执行一遍依赖注入，因为玩家被生成后，依赖注入可能无法执行
        ObjectInjectProvider.Instance.InjectWithChildren(gameObject);
    }

    private void Update()
    {
        // 仅在本地玩家上执行输入和控制逻辑
        if (!isLocalPlayer || !isReady) return;

        // 检查玩家是否着地
        isGrounded = Physics.CheckSphere(transform.position, playerDataConfig.PlayerConfigData.GroundCheckRadius, gameDataConfig.GameConfigData.GroundSceneLayer);

        // 处理玩家移动输入
        var moveHorizontal = Input.GetAxis("Horizontal");
        var moveVertical = Input.GetAxis("Vertical");

        movement = new Vector3(moveHorizontal, 0f, moveVertical).normalized;
        movement = camera.transform.TransformDirection(movement);
        movement.y = 0;
        if (movement.magnitude > 0)
        {
            var targetRotation = Quaternion.LookRotation(movement);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * playerDataConfig.PlayerConfigData.RotateSpeed);

            // 移动玩家
            rb.MovePosition(transform.position + movement * (playerDataConfig.PlayerConfigData.MoveSpeed * Time.deltaTime));

            // 设置奔跑动画
            animator.SetBool("IsRunning", true);
        }
        else
        {
            // 设置闲置动画
            animator.SetBool("IsRunning", false);
        }

        // 处理玩家跳跃输入
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.AddForce(Vector3.up * playerDataConfig.PlayerConfigData.JumpSpeed, ForceMode.Impulse);
            animator.SetTrigger("Jump");
            isJumpTriggered = true;
        }

        // 同步玩家动画状态
        SyncAnimations();
    }

    [ClientCallback]
    private void SyncAnimations()
    {
        // 同步玩家动画状态到服务器
        animator.SetBool("IsRunning", animator.GetBool("IsRunning"));
        if (!isJumpTriggered) return;
        animator.SetTrigger("Jump");
        isJumpTriggered = false;
    }
}

//
// [Command]
// private void CmdProcessMovement(float moveHorizontal, float moveVertical, bool jumpPressed)
// {
//     // 服务器处理移动和跳跃
//     Vector3 movement = new Vector3(moveHorizontal, 0f, moveVertical).normalized;
//     bool isGrounded = Physics.CheckSphere(transform.position, playerDataConfig.PlayerConfigData.GroundCheckRadius, gameDataConfig.GameConfigData.GroundSceneLayer);
//
//     if (movement.magnitude > 0)
//     {
//         MovePlayer(movement);
//     }
//
//     if (jumpPressed && isGrounded)
//     {
//         Jump();
//     }
//
//     UpdateAnimations(movement.magnitude > 0);
// }
//
// [Server]
// private void MovePlayer(Vector3 movement)
// {
//     transform.Translate(movement * playerDataConfig.PlayerConfigData.MoveSpeed * Time.deltaTime);
// }
//
// [Server]
// private void Jump()
// {
//     rb.AddForce(Vector3.up * playerDataConfig.PlayerConfigData.JumpForce, ForceMode.Impulse);
//     RpcAnimateJump();
// }
//
// [Server]
// private void UpdateAnimations(bool isRunning)
// {
//     RpcSetRunning(isRunning);
// }
//
// [ClientRpc]
// private void RpcSetRunning(bool isRunning)
// {
//     animator.SetBool("IsRunning", isRunning);
// }
//
// [ClientRpc]
// private void RpcAnimateJump()
// {
//     animator.SetTrigger("Jump");
// }
//