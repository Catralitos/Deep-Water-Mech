using System;
using _Scripts.Combat;
using _Scripts.Managers;
using _Scripts.MechaParts;
using _Scripts.MechaParts.SO;
using Extensions;
using UnityEngine;

namespace _Scripts.Controller
{
    public class MechaController : MonoBehaviour
    {
        public static MechaController Instance { get; private set; }

        public Head Head { get; set; }
        public Arm LeftArm { get; set; }
        public Arm RightArm { get; set; }
        public Torso Torso { get; set; }
        public Legs Legs { get; set; }
        public BonusPart BonusPart { get; set; }

        [SerializeField] private float gravityValue = -9.81f;
        [SerializeField] private Camera gameCamera;
        [SerializeField] private LayerMask raycastLayerMask;
        [SerializeField] private LayerMask hittableLayerMask;

        [SerializeField] private float medianWeight;
        [SerializeField] private float meleeAttackRange;
        
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Animator mechaAnimator;
        
        [Header("Inventory SO")] 
        [SerializeField] private Inventory inventory;

        [Header("Mech Parts Positions")] 
        [SerializeField] private Transform leftArmTransform;
        [SerializeField] private Transform rightArmTransform;
        [SerializeField] private Transform torsoTransform;
        [SerializeField] private Transform legsTransform;
        [SerializeField] private Transform bonusPartTransform;
        
        private CharacterController _controller;
        private InputManager _inputManager;
        private Transform _leftArmSpawnPoint, _rightArmSpawnPoint;
        private Vector3 _mechaVelocity, _move;
        private bool _groundedMecha, _leftFiring, _rightFiring, _dashing;
        [HideInInspector] public int maxHp, currentHp, currentWeight, maxBoost;
        [HideInInspector] public float currentBoost;
        private float _leftArmCooldownLeft, _rightArmCooldownLeft;
        private static readonly int Moving = Animator.StringToHash("Moving");

        private void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError($"There's more than one MechaController! {transform} - {Instance}");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _controller = GetComponent<CharacterController>();
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
        
        private void Start()
        {
            _inputManager = InputManager.Instance;
            _inputManager.OnLeftAction += OnLeftAction;
            _inputManager.OnLeftActionReleased += OnLeftActionReleased;
            _inputManager.OnRightAction += OnRightAction;
            _inputManager.OnRightActionReleased += OnRightActionReleased;
            _inputManager.OnJumpAction += OnJumpAction;
            _inputManager.OnJumpActionReleased += OnJumpActionReleased;
            _inputManager.OnInteractAction += OnInteractAction;
            _inputManager.OnDashAction += OnDashAction;
            _inputManager.OnDashActionReleased += OnDashActionReleased;
            _controller = GetComponent<CharacterController>();

            //TODO remove this later as it should happen in the game manager
            inventory.InitiateInventory();
            
            Head = inventory.equippedHead;
            LeftArm = inventory.equippedLeftArm;
            RightArm = inventory.equippedRightArm;
            Torso = inventory.equippedTorso;
            Legs = inventory.equippedLegs;
            BonusPart = inventory.equippedBonusPart;

            _leftArmCooldownLeft = 0;
            _rightArmCooldownLeft = 0;
            
            maxHp = GetMaxHp();
            currentHp = maxHp;
            maxBoost = 100;
            currentBoost = maxBoost;
            UpdateMech();
        }

        private void OnDestroy()
        {
            _inputManager.OnLeftAction -= OnLeftAction;
            _inputManager.OnLeftActionReleased -= OnLeftActionReleased;
            _inputManager.OnRightAction -= OnRightAction;
            _inputManager.OnRightActionReleased -= OnRightActionReleased;
            _inputManager.OnJumpAction -= OnJumpAction;
            _inputManager.OnJumpActionReleased -= OnJumpActionReleased;
            _inputManager.OnInteractAction -= OnInteractAction;
            _inputManager.OnDashAction -= OnDashAction;
            _inputManager.OnDashActionReleased -= OnDashActionReleased;
        }

        //call when mech parts are changed out
        private void Update()
        {
            //if (!GameManager.Instance.IsInsideMecha) return;
            HandleMovement();
            HandleAttack();
            
            if (_dashing)
                currentBoost = Math.Clamp(currentBoost - ((BoostPart)BonusPart).boostConsumption * Time.deltaTime, 0, maxBoost);
            else
                currentBoost =  Math.Clamp(currentBoost + ((BoostPart)BonusPart).boostRecovery * Time.deltaTime, 0, maxBoost);
            
        }

        public void UpdateMech()
        {
            //Assemble the mech
            /*
            Instantiate(LeftArm.prefab, leftArmTransform.position, Quaternion.identity);
            Instantiate(RightArm.prefab, rightArmTransform.position, Quaternion.identity);
            Instantiate(Torso.prefab, torsoTransform.position, Quaternion.identity);
            Instantiate(Legs.prefab, legsTransform.position, Quaternion.identity);
            if (BonusPart != null)
               Instantiate(BonusPart.prefab, bonusPartTransform.position, Quaternion.identity);
            */
            
            _leftArmSpawnPoint = LeftArm.prefab.GetComponent<ArmBehaviour>().spawnPoint;
            _rightArmSpawnPoint = RightArm.prefab.GetComponent<ArmBehaviour>().spawnPoint;
            float hpLoss = 1.0f * currentHp / maxHp;
            int newMaxHp = GetMaxHp();
            currentHp = Mathf.RoundToInt(newMaxHp * hpLoss);
            maxHp = newMaxHp;
            currentWeight = GetWeight();
            Debug.Log("The mech weighs " + currentWeight + "kg, and has " + currentHp + "/" + maxHp + " HP.");
        }

        private int GetWeight()
        {
            int currentWeight = Head.weight + Torso.weight + LeftArm.weight + RightArm.weight + Legs.weight;
            currentWeight = BonusPart != null ? currentWeight + BonusPart.weight : currentWeight;
            return currentWeight;
        }

        private int GetMaxHp()
        {
            int maxHp = Head.hp + Torso.hp + LeftArm.hp + RightArm.hp + Legs.hp;
            maxHp = BonusPart != null ? maxHp + BonusPart.hp : maxHp;
            return maxHp;
        }

        private void HandleMovement()
        {
            _groundedMecha = _controller.isGrounded;
            if (_groundedMecha && _mechaVelocity.y < 0 && !_dashing)
                _mechaVelocity.y = 0f;

            Vector3 cameraForward = gameCamera.transform.forward;
            Vector3 inputVector = _inputManager.GetPlayerMovement();
            _move = new Vector3(inputVector.x, 0f, inputVector.y);
            _move = cameraForward * _move.z + gameCamera.transform.right * _move.x;
            _move.y = 0;
            transform.forward = new Vector3(cameraForward.x, 0f, cameraForward.z);
            float moveSpeed = Legs.speed * (medianWeight / currentWeight) * Time.deltaTime;
            if (_dashing) moveSpeed *= ((BoostPart)BonusPart).boostForce;
            _controller.Move(_move * moveSpeed);
            _mechaVelocity.y += gravityValue * Time.deltaTime;
            _controller.Move(_mechaVelocity * Time.deltaTime);
            
            mechaAnimator.SetBool(Moving, inputVector.magnitude != 0);
        }

        private void HandleAttack()
        {
            _leftArmCooldownLeft -= Time.deltaTime;
            _rightArmCooldownLeft -= Time.deltaTime;
            if (_leftFiring)
            {
                switch (LeftArm.type)
                {
                    case ArmType.PROJECTILE:
                        UseProjectile(_leftArmSpawnPoint.position, true);
                        break;
                    case ArmType.HITSCAN:
                        UseHitscan(_leftArmSpawnPoint.position, true);
                        break;
                    case ArmType.MELEE:
                        UseMelee(_leftArmSpawnPoint.position, true);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (_rightFiring)
            {
                switch (RightArm.type)
                {
                    case ArmType.PROJECTILE:
                        UseProjectile(_rightArmSpawnPoint.position, false);
                        break;
                    case ArmType.HITSCAN:
                        UseHitscan(_rightArmSpawnPoint.position, false);
                        break;
                    case ArmType.MELEE:
                        UseMelee(_rightArmSpawnPoint.position, false);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
        
        private void OnLeftAction(object sender, EventArgs e)
        {
            //if (!GameManager.Instance.IsInsideMecha) return;
            _leftFiring = true;
        }
        
        private void OnLeftActionReleased(object sender, EventArgs e)
        {
            //if (!GameManager.Instance.IsInsideMecha) return;
            _leftFiring = false;
        }
        
        private void OnRightAction(object sender, EventArgs e)
        {
            //if (!GameManager.Instance.IsInsideMecha) return;
            _rightFiring = true;
        }
        
        private void OnRightActionReleased(object sender, EventArgs e)
        {
            //if (!GameManager.Instance.IsInsideMecha) return;
            _rightFiring = false;
        }

        private void UseProjectile(Vector3 spawnPoint, bool left)
        {
            switch (left)
            {
                case true when _leftArmCooldownLeft > 0:
                case false when _rightArmCooldownLeft > 0:
                    return;
            }

            Ray ray = gameCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0.0f));
            Vector3 targetPoint = Physics.Raycast(ray, out var hit, raycastLayerMask) ? hit.point : ray.GetPoint(75);
            Vector3 direction = targetPoint - spawnPoint;
            Projectile projectile = Instantiate(projectilePrefab, spawnPoint, Quaternion.identity).GetComponent<Projectile>();
            projectile.gameObject.transform.forward = direction.normalized;
            projectile.body.AddForce(direction.normalized * projectile.projectileSpeed, ForceMode.Impulse);
            projectile.projectileDamage = left ? LeftArm.damage : RightArm.damage;

            if (left) _leftArmCooldownLeft = LeftArm.cooldown;
            else _rightArmCooldownLeft = RightArm.cooldown;
        }

        private void UseHitscan(Vector3 spawnPoint, bool left)
        {
            switch (left)
            {
                case true when _leftArmCooldownLeft > 0:
                case false when _rightArmCooldownLeft > 0:
                    return;
            }
            
            Ray ray = gameCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0.0f));
            if (Physics.Raycast(ray, out var hit, hittableLayerMask))
            {
                int damage = left ? LeftArm.damage : RightArm.damage;
                hit.collider.gameObject.GetComponent<Hittable>().DoDamage(damage);
            }
            //display muzzle at spawnPoint
            
            if (left) _leftArmCooldownLeft = LeftArm.cooldown;
            else _rightArmCooldownLeft = RightArm.cooldown;
        }
        
        private void UseMelee(Vector3 spawnPoint, bool left)
        {
            switch (left)
            {
                case true when _leftArmCooldownLeft > 0:
                case false when _rightArmCooldownLeft > 0:
                    return;
            }            
            
            Collider[] cols = Physics.OverlapSphere(spawnPoint, meleeAttackRange, raycastLayerMask);

            foreach (Collider col in cols)
            {
                if (hittableLayerMask.HasLayer(col.gameObject.layer))
                {
                    int damage = left ? LeftArm.damage : RightArm.damage;
                    col.gameObject.GetComponent<Hittable>().DoDamage(damage);
                }
            }
            
            if (left) _leftArmCooldownLeft = LeftArm.cooldown;
            else _rightArmCooldownLeft = RightArm.cooldown;
            
        }
        
        private void OnJumpAction(object sender, EventArgs e)
        {
            //if (!GameManager.Instance.IsInsideMecha) return;
            if (_groundedMecha)
                if (_dashing)
                    _mechaVelocity.y = Mathf.Sqrt(Legs.jumpPower * ((BoostPart)BonusPart).boostJumpForce * (medianWeight / currentWeight) * -2f * gravityValue);
                else
                    _mechaVelocity.y = Mathf.Sqrt(Legs.jumpPower * (medianWeight / currentWeight) * -2f * gravityValue);
        }
        
        private void OnJumpActionReleased(object sender, EventArgs e)
        {
        }

        private void OnInteractAction(object sender, EventArgs e)
        {
            //if (!GameManager.Instance.IsInsideMecha) return;
            //GameManager.Instance.ExitMecha();
        }

        private void OnDashAction(object sender, EventArgs e)
        {
            //if (!GameManager.Instance.IsInsideMecha) return;
            if (BonusPart != null && BonusPart is BoostPart && currentBoost > 0)
               _dashing = true;
        } 
        
        private void OnDashActionReleased(object sender, EventArgs e)
        {
            //if (!GameManager.Instance.IsInsideMecha) return;
            _dashing = false;
        } 
    }
}