using UnityEngine;

namespace SpiderPlayground
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class SpiderController : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;
        [SerializeField] private Transform firstPersonLeftHand;
        [SerializeField] private Transform firstPersonRightHand;
        [SerializeField] private Renderer[] bodyRenderers;
        [SerializeField] private Renderer[] firstPersonHandRenderers;
        [SerializeField] private Material leftWebMaterial;
        [SerializeField] private Material rightWebMaterial;

        [Header("Movement")]
        [SerializeField] private float groundSpeed = 10f;
        [SerializeField] private float airAcceleration = 32f;
        [SerializeField] private float jumpSpeed = 9f;
        [SerializeField] private float gravityMultiplier = 2f;
        [SerializeField] private float mouseSensitivity = 2.1f;

        [Header("Wall climbing")]
        [SerializeField] private float wallClimbSpeed = 6f;
        [SerializeField] private float wallSideSpeed = 4f;
        [SerializeField] private float wallDetachSpeed = 7f;
        [SerializeField] private float roofVaultDuration = 0.64f;

        [Header("Webs")]
        [SerializeField] private float webRange = 110f;
        [SerializeField] private float webShotSpeed = 250f;
        [SerializeField] private float webMissDistance = 55f;
        [SerializeField] private float swingSideSpeedMultiplier = 3.3f;
        [SerializeField] private float swingSideAcceleration = 95f;
        [SerializeField] private float swingSideBurst = 4.5f;
        [SerializeField] private float swingMaximumSpeed = 42f;
        [SerializeField] private float swingPumpAcceleration = 12f;
        [SerializeField] private float yankSpeed = 38f;
        [SerializeField] private float yankDuration = 0.32f;
        [SerializeField] private float yankMaxDistance = 12f;
        [SerializeField] private float yankWallClearance = 2f;

        private sealed class WebState
        {
            public bool attached;
            public bool firing;
            public bool swingMode;
            public bool willAttach;
            public Vector3 anchor;
            public Vector3 shotOrigin;
            public Vector3 shotTip;
            public float shotElapsed;
            public float shotDuration;
            public float ropeLength;
            public LineRenderer line;
            public Transform projectileTip;
            public Collider anchorCollider;
            public Vector3 anchorNormal;
        }

        private readonly WebState leftWeb = new WebState();
        private readonly WebState rightWeb = new WebState();
        private readonly RaycastHit[] rayHits = new RaycastHit[32];
        private readonly RaycastHit[] cameraHits = new RaycastHit[32];
        private readonly RaycastHit[] groundHits = new RaycastHit[16];
        private readonly RaycastHit[] wallHits = new RaycastHit[16];
        private readonly RaycastHit[] webOcclusionHits = new RaycastHit[32];
        private Rigidbody body;
        private SpiderProceduralAnimator proceduralAnimator;
        private SpiderFirstPersonHands firstPersonRig;
        private float yaw;
        private float pitch;
        private float horizontalInput;
        private float verticalInput;
        private bool jumpQueued;
        private bool nextShotFromLeft = true;
        private bool yankActive;
        private bool yankFromLeft;
        private float yankElapsed;
        private float yankTravelRemaining;
        private float shotPoseUntil;
        private bool shotPoseFromLeft;
        private float yankPoseUntil;
        private Vector3 yankDirection;
        private WebState yankWeb;
        private bool firstPerson;
        private bool grounded;
        private bool wallClimbing;
        private bool wallAtLedge;
        private bool roofVaulting;
        private Vector3 wallNormal;
        private Vector3 wallContactPoint;
        private Vector3 ledgeRoofPoint;
        private Vector3 vaultStart;
        private Vector3 vaultTarget;
        private float vaultClearanceY;
        private float vaultElapsed;
        private WebAttachSurface wallSurface;
        private float wallDetachUntil;
        private WebAttachSurface wallYankDepartureSurface;
        private Vector3 wallYankDepartureNormal;
        private float lastGroundJumpTime = -10f;
        private int lastSwingSideInput;
        private float lastSwingBurstTime = -10f;
        private GUIStyle hudStyle;
        private GUIStyle crosshairStyle;

        public void Configure(
            Camera cameraToUse,
            Transform left,
            Transform right,
            Transform firstLeft,
            Transform firstRight,
            Renderer[] thirdPersonRenderers,
            Renderer[] firstPersonRenderers,
            Material leftLineMaterial,
            Material rightLineMaterial)
        {
            viewCamera = cameraToUse;
            leftHand = left;
            rightHand = right;
            firstPersonLeftHand = firstLeft;
            firstPersonRightHand = firstRight;
            bodyRenderers = thirdPersonRenderers;
            firstPersonHandRenderers = firstPersonRenderers;
            leftWebMaterial = leftLineMaterial;
            rightWebMaterial = rightLineMaterial;
        }

        private void Awake()
        {
            EnsureDemoRoofCollider();
            body = GetComponent<Rigidbody>();
            yaw = transform.eulerAngles.y;
            proceduralAnimator = GetComponent<SpiderProceduralAnimator>();
            if (proceduralAnimator == null)
                proceduralAnimator = gameObject.AddComponent<SpiderProceduralAnimator>();
            proceduralAnimator.Initialize(transform.Find("Simple Faceless Body"), leftHand, rightHand);
            if (viewCamera != null && firstPersonLeftHand != null &&
                firstPersonRightHand != null)
            {
                Material gloveMaterial = firstPersonLeftHand.GetComponent<Renderer>().sharedMaterial;
                firstPersonRig = new SpiderFirstPersonHands(viewCamera.transform,
                    firstPersonLeftHand, firstPersonRightHand,
                    gloveMaterial, leftWebMaterial);
            }
            SetFirstPerson(false);
        }

        private void OnDestroy()
        {
            if (firstPersonRig != null) firstPersonRig.Dispose();
        }

        private static void EnsureDemoRoofCollider()
        {
            GameObject building = GameObject.Find("Central Block - Replace with Building Later");
            if (building == null) return;
            Transform oldRoof = building.transform.Find("Roof Edge");
            Material roofMaterial = oldRoof == null ? building.GetComponent<Renderer>().sharedMaterial :
                oldRoof.GetComponent<Renderer>().sharedMaterial;
            if (oldRoof != null) oldRoof.gameObject.SetActive(false);
            if (building.transform.Find("Roof Surface") != null) return;

            // A single render-only plane sits slightly above the solid building.
            // It has no side collider or overlapping coplanar faces.
            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
            surface.name = "Roof Surface";
            surface.transform.position = new Vector3(building.transform.position.x,
                building.GetComponent<Collider>().bounds.max.y + 0.012f,
                building.transform.position.z);
            surface.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            surface.transform.localScale = new Vector3(building.transform.lossyScale.x,
                building.transform.lossyScale.z, 1f);
            surface.transform.SetParent(building.transform, true);
            Renderer renderer = surface.GetComponent<Renderer>();
            renderer.sharedMaterial = roofMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            Destroy(surface.GetComponent<Collider>());
        }

        private void Start()
        {
            LockCursor();
            UpdateCamera();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                horizontalInput = 0f;
                verticalInput = 0f;
                jumpQueued = false;
                if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
                    LockCursor();
                return;
            }

            yaw += Input.GetAxisRaw("Mouse X") * mouseSensitivity;
            pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * mouseSensitivity, -80f, 80f);

            horizontalInput = (Input.GetKey(KeyCode.D) ? 1f : 0f) -
                              (Input.GetKey(KeyCode.A) ? 1f : 0f);
            verticalInput = (Input.GetKey(KeyCode.W) ? 1f : 0f) -
                            (Input.GetKey(KeyCode.S) ? 1f : 0f);
            if (Input.GetKeyDown(KeyCode.Space)) jumpQueued = true;

            if (Input.GetKeyDown(KeyCode.V)) SetFirstPerson(!firstPerson);
            if (Input.GetKeyDown(KeyCode.R)) Respawn();

            UpdateCamera();
            if (Input.GetMouseButtonDown(0)) FireAlternatingWeb();
            if (Input.GetMouseButtonDown(1)) BeginYank();
        }

        private void FixedUpdate()
        {
            if (roofVaulting)
            {
                AdvanceRoofVault();
                jumpQueued = false;
                return;
            }

            grounded = IsGrounded();
            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

            if (wallClimbing)
            {
                bool roofFound = FindRoofLedge(out Vector3 roofPoint);
                if (roofFound && body.position.y >= roofPoint.y - 1.23f)
                {
                    wallAtLedge = true;
                    ledgeRoofPoint = roofPoint;
                }
                else if (wallAtLedge && (!roofFound || body.position.y < ledgeRoofPoint.y - 1.33f))
                    wallAtLedge = false;

                if (jumpQueued)
                {
                    if (wallAtLedge) BeginRoofVault();
                    else DetachWall();
                }
                else if (wallAtLedge)
                {
                    // Hold at the side of the roof until Space starts the vault.
                }
                else if (FindWall(-wallNormal, out RaycastHit heldWall))
                {
                    wallNormal = heldWall.normal;
                    wallContactPoint = heldWall.point;
                }
                else
                    LeaveWall();
            }

            if (roofVaulting)
            {
                AdvanceRoofVault();
                jumpQueued = false;
                return;
            }

            if (!wallClimbing && !yankActive && verticalInput > 0.1f &&
                Time.time >= wallDetachUntil && FindWall(forward, out RaycastHit newWall))
                BeginWall(newWall);

            if (wallClimbing)
            {
                MoveOnWall();
                jumpQueued = false;
                if (body.position.y < -20f) Respawn();
                return;
            }

            if (yankActive)
            {
                body.useGravity = false;
                grounded = false;
                body.MoveRotation(Quaternion.Euler(0f, yaw, 0f));
                AdvanceYank();
                jumpQueued = false;
                if (body.position.y < -20f) Respawn();
                return;
            }

            body.useGravity = true;
            body.MoveRotation(Quaternion.Euler(0f, yaw, 0f));
            if (body.useGravity && gravityMultiplier > 1f)
                body.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
            Vector3 movement = Vector3.ClampMagnitude(
                forward * verticalInput + right * horizontalInput, 1f);

            bool startingGroundJump = jumpQueued && grounded;
            if (startingGroundJump)
            {
                // Do not carry the takeoff key's ground impulse into air steering.
                // The held key can steer again from the next physics step, at the
                // same maximum horizontal speed as on the ground.
                body.linearVelocity = Vector3.up * jumpSpeed;
                lastGroundJumpTime = Time.time;
                grounded = false;
            }
            else if (grounded)
            {
                Vector3 horizontalVelocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
                Vector3 change = movement * groundSpeed - horizontalVelocity;
                body.AddForce(Vector3.ClampMagnitude(change, 30f * Time.fixedDeltaTime), ForceMode.VelocityChange);
            }
            else if (HasSwingingWeb())
            {
                ApplySwingSteering(forward, right);
            }
            else if (movement.sqrMagnitude > 0.01f)
            {
                Vector3 horizontalVelocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
                Vector3 steering = movement * groundSpeed - horizontalVelocity;
                body.AddForce(Vector3.ClampMagnitude(steering,
                    airAcceleration * Time.fixedDeltaTime), ForceMode.VelocityChange);
            }
            if (grounded || !HasSwingingWeb()) lastSwingSideInput = 0;
            jumpQueued = false;

            ApplyWebForce(leftWeb);
            ApplyWebForce(rightWeb);
            if (!grounded && HasSwingingWeb()) ApplySwingPump();

            if (body.position.y < -20f) Respawn();
        }

        private void LateUpdate()
        {
            UpdateCamera();
            SpiderMotionState motionState = GetMotionState();
            if (proceduralAnimator != null)
                proceduralAnimator.Animate(motionState, body.linearVelocity,
                    leftWeb.attached, leftWeb.anchor, rightWeb.attached, rightWeb.anchor);
            AnimateFirstPersonHands(motionState);
            AdvanceWebShot(leftWeb, true);
            AdvanceWebShot(rightWeb, false);
            UpdateWebLine(leftWeb, true);
            UpdateWebLine(rightWeb, false);
        }

        private void AnimateFirstPersonHands(SpiderMotionState state)
        {
            if (firstPersonRig == null) return;
            float shotProgress = Time.time < shotPoseUntil ?
                1f - (shotPoseUntil - Time.time) / 0.28f : -1f;
            float yankProgress = Time.time < yankPoseUntil ?
                1f - (yankPoseUntil - Time.time) / 0.42f : -1f;
            firstPersonRig.Animate(state, leftWeb.attached, rightWeb.attached,
                shotProgress, shotPoseFromLeft, yankProgress, yankFromLeft,
                wallContactPoint, wallNormal, body.linearVelocity);
        }

        private SpiderMotionState GetMotionState()
        {
            if (roofVaulting) return SpiderMotionState.Jumping;
            if (wallClimbing) return SpiderMotionState.WallClimbing;
            if (Time.time - lastGroundJumpTime < 0.48f && body.linearVelocity.y > 0f)
                return SpiderMotionState.Jumping;
            if ((leftWeb.attached || rightWeb.attached) && !grounded)
                return SpiderMotionState.WebHanging;
            if (grounded)
                return new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude > 0.6f
                    ? SpiderMotionState.Walking : SpiderMotionState.Idle;
            return SpiderMotionState.Airborne;
        }

        private bool FindWall(Vector3 direction, out RaycastHit nearestWall)
        {
            nearestWall = new RaycastHit();
            Vector3 origin = body.position + Vector3.up * 1.15f;
            int count = Physics.RaycastNonAlloc(origin, direction, wallHits, 0.95f,
                ~0, QueryTriggerInteraction.Ignore);
            float nearestDistance = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = wallHits[i];
                if (hit.collider.GetComponentInParent<SpiderController>() == this) continue;
                if (hit.collider.GetComponentInParent<WebAttachSurface>() == null) continue;
                if (Mathf.Abs(hit.normal.y) > 0.35f || hit.distance >= nearestDistance) continue;
                nearestDistance = hit.distance;
                nearestWall = hit;
                found = true;
            }
            return found;
        }

        private void BeginWall(RaycastHit wall)
        {
            BeginWall(wall.collider, wall.normal, wall.point);
        }

        private void BeginWall(Collider wallCollider, Vector3 normal, Vector3 contactPoint)
        {
            wallClimbing = true;
            yankPoseUntil = 0f;
            wallYankDepartureSurface = null;
            wallAtLedge = false;
            wallNormal = normal;
            wallContactPoint = contactPoint;
            wallSurface = wallCollider.GetComponentInParent<WebAttachSurface>();
            // Keep the visible hands and torso outside the wall, not just the capsule.
            float distance = Vector3.Dot(body.position - wallContactPoint, wallNormal);
            if (distance < 0.65f)
                body.position += wallNormal * (0.65f - distance);
            body.useGravity = false;
            body.linearVelocity = Vector3.zero;
            grounded = false;
            if (leftWeb.attached && leftWeb.anchorCollider != null &&
                leftWeb.anchorCollider.GetComponentInParent<WebAttachSurface>() == wallSurface)
                ReleaseWeb(leftWeb, true, true);
            if (rightWeb.attached && rightWeb.anchorCollider != null &&
                rightWeb.anchorCollider.GetComponentInParent<WebAttachSurface>() == wallSurface)
                ReleaseWeb(rightWeb, false, true);
        }

        private void MoveOnWall()
        {
            if (wallAtLedge)
            {
                Vector3 position = body.position;
                position.y = Mathf.Min(position.y, ledgeRoofPoint.y - 1.15f);
                body.position = position;
            }
            Vector3 wallRight = Vector3.Cross(wallNormal, Vector3.up).normalized;
            body.MoveRotation(Quaternion.LookRotation(-wallNormal, Vector3.up));
            float climbInput = wallAtLedge ? Mathf.Min(verticalInput, 0f) : verticalInput;
            float distance = Vector3.Dot(body.position - wallContactPoint, wallNormal);
            if (distance < 0.59f)
            {
                body.position += wallNormal * (0.59f - distance);
                distance = 0.59f;
            }
            float wallCorrection = Mathf.Clamp((0.65f - distance) * 10f, -2f, 2f);
            body.linearVelocity = Vector3.up * (climbInput * wallClimbSpeed) +
                                  wallRight * (horizontalInput * wallSideSpeed) +
                                  wallNormal * wallCorrection;

            // Keep attached strands slack while the player deliberately climbs.
            RelaxWebForClimbing(leftWeb);
            RelaxWebForClimbing(rightWeb);
        }

        private void RelaxWebForClimbing(WebState web)
        {
            if (web.attached)
                web.ropeLength = Mathf.Max(web.ropeLength,
                    Vector3.Distance(body.position + Vector3.up, web.anchor) + 0.2f);
        }

        private void DetachWall()
        {
            Vector3 away = wallNormal;
            wallYankDepartureSurface = null;
            LeaveWall();
            body.linearVelocity = away * wallDetachSpeed + Vector3.up * 2.5f;
            grounded = false;
            wallDetachUntil = Time.time + 0.55f;
            jumpQueued = false;
        }

        private void LeaveWall()
        {
            wallClimbing = false;
            wallAtLedge = false;
            wallSurface = null;
            body.useGravity = true;
        }

        private bool FindRoofLedge(out Vector3 roofPoint)
        {
            roofPoint = Vector3.zero;
            if (wallSurface == null) return false;

            // Search above and just inside the same wall for its roof surface.
            Vector3 origin = body.position - wallNormal * 0.95f + Vector3.up * 2.25f;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, wallHits, 3.1f,
                ~0, QueryTriggerInteraction.Ignore);
            float bestHeight = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = wallHits[i];
                if (hit.collider.GetComponentInParent<SpiderController>() == this) continue;
                if (hit.collider.GetComponentInParent<WebAttachSurface>() != wallSurface) continue;
                if (hit.normal.y < 0.65f || hit.point.y < body.position.y + 0.55f) continue;
                if (hit.point.y <= bestHeight) continue;
                bestHeight = hit.point.y;
                roofPoint = hit.point;
            }
            if (bestHeight > float.NegativeInfinity) return true;

            // A single solid building has no separate roof collider. Raycast from
            // above its bounding box, because a ray started inside a box misses it.
            Collider wallCollider = wallSurface.GetComponent<Collider>();
            if (wallCollider == null) return false;
            Vector3 inside = body.position - wallNormal * 0.95f;
            Bounds bounds = wallCollider.bounds;
            if (inside.x < bounds.min.x + 0.05f || inside.x > bounds.max.x - 0.05f ||
                inside.z < bounds.min.z + 0.05f || inside.z > bounds.max.z - 0.05f)
                return false;
            Ray above = new Ray(new Vector3(inside.x, bounds.max.y + 0.25f, inside.z),
                Vector3.down);
            if (!wallCollider.Raycast(above, out RaycastHit top, 1.5f) ||
                top.normal.y < 0.65f || top.point.y < body.position.y + 0.55f ||
                top.point.y > body.position.y + 2.25f)
                return false;
            roofPoint = top.point;
            return true;
        }

        private void BeginRoofVault()
        {
            wallYankDepartureSurface = null;
            wallClimbing = false;
            wallAtLedge = false;
            wallSurface = null;
            roofVaulting = true;
            vaultElapsed = 0f;
            vaultStart = body.position;
            vaultClearanceY = ledgeRoofPoint.y + 0.55f;
            vaultTarget = ledgeRoofPoint - wallNormal * 0.55f + Vector3.up * 0.1f;
            body.linearVelocity = Vector3.zero;
            body.isKinematic = true;
            body.useGravity = false;
            grounded = false;
            jumpQueued = false;
        }

        private void AdvanceRoofVault()
        {
            vaultElapsed += Time.fixedDeltaTime;
            float progress = Mathf.Clamp01(vaultElapsed / Mathf.Max(0.1f, roofVaultDuration));
            Vector3 clearance = new Vector3(vaultStart.x, vaultClearanceY, vaultStart.z);
            Vector3 position;
            if (progress < 0.52f)
            {
                float up = Mathf.SmoothStep(0f, 1f, progress / 0.52f);
                position = Vector3.Lerp(vaultStart, clearance, up);
            }
            else
            {
                float over = Mathf.SmoothStep(0f, 1f, (progress - 0.52f) / 0.48f);
                position = Vector3.Lerp(clearance, vaultTarget, over);
            }
            body.MovePosition(position);
            RelaxWebForClimbing(leftWeb);
            RelaxWebForClimbing(rightWeb);

            if (progress < 1f) return;

            body.position = vaultTarget;
            body.isKinematic = false;
            body.useGravity = true;
            body.linearVelocity = -wallNormal * 1.5f;
            roofVaulting = false;
            wallDetachUntil = Time.time + 0.55f;
        }

        private void FireAlternatingWeb()
        {
            // First click shoots, the next click releases. Only the following
            // click shoots from the opposite hand.
            if (leftWeb.attached || leftWeb.firing ||
                rightWeb.attached || rightWeb.firing)
            {
                if (leftWeb.attached || leftWeb.firing) ReleaseWeb(leftWeb, true);
                if (rightWeb.attached || rightWeb.firing) ReleaseWeb(rightWeb, false);
                return;
            }
            bool isLeft = nextShotFromLeft;
            nextShotFromLeft = !nextShotFromLeft;
            FireWeb(isLeft ? leftWeb : rightWeb, isLeft);
            shotPoseFromLeft = isLeft;
            shotPoseUntil = Time.time + 0.28f;
            yankPoseUntil = 0f;
            if (proceduralAnimator != null) proceduralAnimator.PlayWebShot(isLeft);
        }

        private void FireWeb(WebState web, bool isLeft)
        {
            int hitCount = Physics.RaycastNonAlloc(viewCamera.transform.position,
                viewCamera.transform.forward, rayHits, webRange, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            RaycastHit target = new RaycastHit();
            bool found = false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = rayHits[i];
                if (hit.collider.GetComponentInParent<SpiderController>() == this) continue;
                if (hit.distance >= nearest) continue;
                nearest = hit.distance;
                target = hit;
                found = true;
            }

            web.shotOrigin = GetWebOrigin(isLeft);
            Vector3 aimedPoint = found ? target.point :
                viewCamera.transform.position + viewCamera.transform.forward * webMissDistance;
            // The camera and hand are in different places. Use the first surface
            // actually crossed by the strand leaving the hand.
            if (TryFindWebBlocker(web.shotOrigin, aimedPoint, out RaycastHit blocker))
            {
                target = blocker;
                found = true;
            }
            web.willAttach = found &&
                target.collider.GetComponentInParent<WebAttachSurface>() != null;
            web.anchorCollider = web.willAttach ? target.collider : null;
            web.anchorNormal = web.willAttach ? target.normal : Vector3.zero;
            web.anchor = found ? target.point : aimedPoint;
            web.shotTip = web.shotOrigin;
            web.shotElapsed = 0f;
            web.shotDuration = Mathf.Clamp(
                Vector3.Distance(web.shotOrigin, web.anchor) / webShotSpeed, 0.045f, 0.3f);
            web.firing = true;
            web.swingMode = false;

            GameObject lineObject = new GameObject(isLeft ? "Left Web" : "Right Web");
            web.line = lineObject.AddComponent<LineRenderer>();
            web.line.sharedMaterial = isLeft ? leftWebMaterial : rightWebMaterial;
            web.line.useWorldSpace = true;
            web.line.positionCount = 2;
            web.line.widthMultiplier = 0.045f;
            web.line.numCapVertices = 4;
            web.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            web.line.receiveShadows = false;
            Color webColor = Color.white;
            web.line.startColor = webColor;
            web.line.endColor = webColor;
            GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tip.name = isLeft ? "Left Web Projectile" : "Right Web Projectile";
            tip.transform.SetParent(lineObject.transform, true);
            tip.transform.position = web.shotOrigin;
            tip.transform.localScale = Vector3.one * 0.13f;
            tip.GetComponent<Renderer>().sharedMaterial = web.line.sharedMaterial;
            Collider tipCollider = tip.GetComponent<Collider>();
            tipCollider.enabled = false;
            Destroy(tipCollider);
            web.projectileTip = tip.transform;
            UpdateWebLine(web, isLeft);
        }

        private void AdvanceWebShot(WebState web, bool isLeft)
        {
            if (!web.firing) return;
            web.shotElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(web.shotElapsed / web.shotDuration);
            web.shotTip = Vector3.Lerp(web.shotOrigin, web.anchor, progress);
            if (web.projectileTip != null) web.projectileTip.position = web.shotTip;
            if (progress < 1f) return;

            web.firing = false;
            if (web.projectileTip != null) Destroy(web.projectileTip.gameObject);
            web.projectileTip = null;
            if (!web.willAttach)
            {
                ReleaseWeb(web, isLeft);
                return;
            }

            web.attached = true;
            web.swingMode = true;
            web.ropeLength = Mathf.Max(0.65f,
                Vector3.Distance(body.position + Vector3.up, web.anchor));
            if (!wallClimbing && !roofVaulting) body.useGravity = true;
        }

        private void BeginYank()
        {
            if (yankActive || roofVaulting) return;
            WebState web = leftWeb.attached ? leftWeb :
                rightWeb.attached ? rightWeb : null;
            if (web == null) return;
            if (wallClimbing)
            {
                WebAttachSurface targetSurface = web.anchorCollider == null ? null :
                    web.anchorCollider.GetComponentInParent<WebAttachSurface>();
                if (targetSurface == null ||
                    IsSameWallFace(wallSurface, wallNormal,
                        targetSurface, web.anchorNormal))
                    return;
            }

            Vector3 toAnchor = web.anchor - (body.position + Vector3.up);
            float distance = toAnchor.magnitude;
            if (distance < 0.01f) return;
            Vector3 direction = toAnchor / distance;
            float availableTravel = Mathf.Min(yankMaxDistance,
                Mathf.Max(0f, distance - yankWallClearance));
            if (Mathf.Abs(web.anchorNormal.y) < 0.35f)
            {
                float wallDistance = Vector3.Dot(body.position - web.anchor,
                    web.anchorNormal);
                float approach = -Vector3.Dot(direction, web.anchorNormal);
                if (approach > 0.01f)
                    availableTravel = Mathf.Min(availableTravel,
                        Mathf.Max(0f, (wallDistance - yankWallClearance) / approach));
            }
            if (availableTravel < 0.05f)
            {
                ReleaseWeb(web, web == leftWeb, true);
                return;
            }

            if (wallClimbing)
            {
                // Space-style detachment, followed immediately by the yank.
                // Keep the old wall on cooldown while allowing the destination
                // wall to catch the player before the cooldown expires.
                WebAttachSurface departureSurface = wallSurface;
                Vector3 departureNormal = wallNormal;
                DetachWall();
                wallYankDepartureSurface = departureSurface;
                wallYankDepartureNormal = departureNormal;
            }

            yankWeb = web;
            yankFromLeft = web == leftWeb;
            yankDirection = direction;
            yankTravelRemaining = availableTravel;
            yankElapsed = 0f;
            yankActive = true;
            shotPoseUntil = 0f;
            yankPoseUntil = Time.time + 0.42f;
            body.useGravity = false;
            body.linearVelocity = Vector3.zero;
            if (proceduralAnimator != null) proceduralAnimator.PlayWebYank(yankFromLeft);
        }

        private void AdvanceYank()
        {
            float timeRemaining = Mathf.Max(0f, yankDuration - yankElapsed);
            float step = Mathf.Min(yankTravelRemaining,
                yankSpeed * Mathf.Min(Time.fixedDeltaTime, timeRemaining));
            body.linearVelocity = yankDirection * (step / Time.fixedDeltaTime);
            yankTravelRemaining -= step;
            yankElapsed += Time.fixedDeltaTime;
            if (yankTravelRemaining <= 0.01f || yankElapsed >= yankDuration)
                FinishYank();
        }

        private void FinishYank()
        {
            WebState released = yankWeb;
            bool wasLeft = yankFromLeft;
            yankActive = false;
            yankWeb = null;
            if (released != null && (released.attached || released.firing))
                ReleaseWeb(released, wasLeft, true);
            if (!wallClimbing && !roofVaulting) body.useGravity = true;
        }

        private void ReleaseWeb(WebState web, bool isLeft, bool immediate = false)
        {
            bool stoppedInFlight = web.firing;
            if (!immediate && (web.attached || web.firing) &&
                TryFindWebBlocker(GetWebOrigin(isLeft),
                    web.firing ? web.shotTip : web.anchor, out _))
                immediate = true;
            web.attached = false;
            web.firing = false;
            web.swingMode = false;
            web.willAttach = false;
            web.anchorCollider = null;
            web.anchorNormal = Vector3.zero;
            if (web.projectileTip != null) Destroy(web.projectileTip.gameObject);
            web.projectileTip = null;
            if (web == yankWeb)
            {
                yankActive = false;
                yankWeb = null;
            }
            if (!wallClimbing && !roofVaulting && body != null)
                body.useGravity = true;
            if (web.line == null) return;

            // On wall contact free the whole strand immediately; manual release and
            // air misses get a short visible fade before cleanup.
            if (immediate)
            {
                web.line.enabled = false;
                Destroy(web.line.gameObject);
                web.line = null;
                return;
            }
            UpdateWebLine(web, isLeft, true);
            if (stoppedInFlight) web.line.SetPosition(1, web.shotTip);
            web.line.gameObject.AddComponent<WebFadingLine>().Begin(web.line, Color.white, 0.8f);
            web.line = null;
        }

        private bool HasSwingingWeb()
        {
            return (leftWeb.attached && leftWeb.swingMode) ||
                   (rightWeb.attached && rightWeb.swingMode);
        }

        private void ApplySwingSteering(Vector3 forward, Vector3 right)
        {
            WebState web = leftWeb.attached && leftWeb.swingMode ? leftWeb : rightWeb;
            Vector3 radial = body.position + Vector3.up - web.anchor;
            if (radial.sqrMagnitude < 0.01f) return;
            radial.Normalize();

            // All steering is tangent to the rope, preserving its fixed length.
            Vector3 side = Vector3.ProjectOnPlane(right, radial);
            Vector3 ahead = Vector3.ProjectOnPlane(forward, radial);
            if (side.sqrMagnitude > 0.01f && Mathf.Abs(horizontalInput) > 0.01f)
            {
                side.Normalize();
                float current = Vector3.Dot(body.linearVelocity, side);
                float desired = horizontalInput * groundSpeed * swingSideSpeedMultiplier;
                float change = Mathf.Clamp(desired - current,
                    -swingSideAcceleration * Time.fixedDeltaTime,
                    swingSideAcceleration * Time.fixedDeltaTime);
                body.AddForce(side * change, ForceMode.VelocityChange);

                int direction = horizontalInput > 0f ? 1 : -1;
                if (direction != lastSwingSideInput && Time.time - lastSwingBurstTime > 0.2f)
                {
                    // A short push when starting or reversing a turn makes
                    // lateral swings feel immediate without stretching the web.
                    body.AddForce(side * direction * swingSideBurst,
                        ForceMode.VelocityChange);
                    lastSwingBurstTime = Time.time;
                }
                lastSwingSideInput = direction;
            }
            else
                lastSwingSideInput = 0;

            if (ahead.sqrMagnitude > 0.01f && Mathf.Abs(verticalInput) > 0.01f)
            {
                ahead.Normalize();
                float current = Vector3.Dot(body.linearVelocity, ahead);
                float desired = verticalInput * groundSpeed;
                float change = Mathf.Clamp(desired - current,
                    -airAcceleration * Time.fixedDeltaTime,
                    airAcceleration * Time.fixedDeltaTime);
                body.AddForce(ahead * change, ForceMode.VelocityChange);
            }
        }

        private void ApplySwingPump()
        {
            WebState web = leftWeb.attached && leftWeb.swingMode ? leftWeb : rightWeb;
            Vector3 radial = body.position + Vector3.up - web.anchor;
            if (radial.sqrMagnitude < 0.01f) return;
            Vector3 tangentialVelocity = Vector3.ProjectOnPlane(body.linearVelocity,
                radial.normalized);
            float speed = tangentialVelocity.magnitude;
            if (speed < 1f || speed >= swingMaximumSpeed) return;
            float boost = Mathf.Min(swingPumpAcceleration * Time.fixedDeltaTime,
                swingMaximumSpeed - speed);
            body.AddForce(tangentialVelocity / speed * boost,
                ForceMode.VelocityChange);
        }

        private void ApplyWebForce(WebState web)
        {
            if (!web.attached || !web.swingMode) return;

            // The raycast point is already on the collider surface. Keep the
            // body's rope pivot on a sphere around that exact point so the
            // strand cannot stretch or retract during a swing.
            Vector3 fromAnchor = body.position + Vector3.up - web.anchor;
            float distance = fromAnchor.magnitude;
            if (distance < 0.01f) return;

            Vector3 outward = fromAnchor / distance;
            body.position += outward * (web.ropeLength - distance);
            body.linearVelocity -= outward * Vector3.Dot(body.linearVelocity, outward);
            if (body.linearVelocity.sqrMagnitude > swingMaximumSpeed * swingMaximumSpeed)
                body.linearVelocity = body.linearVelocity.normalized * swingMaximumSpeed;
        }

        private void UpdateWebLine(WebState web, bool isLeft, bool detached = false)
        {
            if (web.line == null || (!web.attached && !web.firing && !detached)) return;
            Vector3 origin = GetWebOrigin(isLeft);
            Vector3 end = web.firing ? web.shotTip : web.anchor;
            if (!detached && TryFindWebBlocker(origin, end, out _))
            {
                // No wrapping system exists: break an obstructed strand before
                // it can render through a building or keep pulling through it.
                ReleaseWeb(web, isLeft, true);
                return;
            }
            web.line.SetPosition(0, origin);
            web.line.SetPosition(1, end);
        }

        private Vector3 GetWebOrigin(bool isLeft)
        {
            if (firstPerson && firstPersonRig != null)
                return firstPersonRig.GetWebOrigin(isLeft,
                    Time.time < yankPoseUntil && yankFromLeft == isLeft);
            return (isLeft ? leftHand : rightHand).position;
        }

        private bool TryFindWebBlocker(Vector3 origin, Vector3 end,
            out RaycastHit nearestHit)
        {
            nearestHit = new RaycastHit();
            Vector3 travel = end - origin;
            float distance = travel.magnitude;
            if (distance <= 0.08f) return false;
            int count = Physics.RaycastNonAlloc(origin, travel / distance,
                webOcclusionHits, distance - 0.06f, ~0,
                QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = webOcclusionHits[i];
                if (hit.collider.GetComponentInParent<SpiderController>() == this ||
                    hit.distance >= nearest) continue;
                nearest = hit.distance;
                nearestHit = hit;
            }
            return nearest < float.PositiveInfinity;
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleWallContact(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            HandleWallContact(collision);
        }

        private void HandleWallContact(Collision collision)
        {
            Collider wall = collision.collider;
            WebAttachSurface surface = wall.GetComponentInParent<WebAttachSurface>();
            if (!wallClimbing && !roofVaulting && !IsGrounded() && surface != null)
            {
                for (int i = 0; i < collision.contactCount; i++)
                {
                    ContactPoint contact = collision.GetContact(i);
                    Vector3 normal = contact.normal;
                    if (Vector3.Dot(body.position - contact.point, normal) < 0f)
                        normal = -normal;
                    if (Mathf.Abs(normal.y) > 0.35f) continue;
                    if (Time.time < wallDetachUntil &&
                        (wallYankDepartureSurface == null ||
                         IsSameWallFace(wallYankDepartureSurface,
                             wallYankDepartureNormal, surface, normal)))
                        continue;

                    // Any airborne side-wall contact becomes a climb, regardless
                    // of web state, input, or camera direction.
                    BeginWall(wall, normal, contact.point);
                    if (leftWeb.attached) ReleaseWeb(leftWeb, true, true);
                    if (rightWeb.attached) ReleaseWeb(rightWeb, false, true);
                    return;
                }
            }

            // A web aimed at a roof still disappears when it reaches that surface.
            if (leftWeb.attached && !leftWeb.swingMode && leftWeb.anchorCollider == wall)
                ReleaseWeb(leftWeb, true, true);
            if (rightWeb.attached && !rightWeb.swingMode && rightWeb.anchorCollider == wall)
                ReleaseWeb(rightWeb, false, true);
        }

        private static bool IsSameWallFace(WebAttachSurface firstSurface,
            Vector3 firstNormal, WebAttachSurface secondSurface, Vector3 secondNormal)
        {
            if (firstSurface != secondSurface) return false;
            if (firstNormal.sqrMagnitude < 0.5f || secondNormal.sqrMagnitude < 0.5f)
                return true;
            return Vector3.Dot(firstNormal.normalized, secondNormal.normalized) > 0.8f;
        }

        private bool IsGrounded()
        {
            if (body.linearVelocity.y > 0.5f) return false;
            Vector3 origin = body.position + Vector3.up * 0.2f;
            int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, 0.35f,
                ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = groundHits[i];
                if (hit.collider.GetComponentInParent<SpiderController>() == this) continue;
                if (hit.normal.y > 0.6f) return true;
            }
            return false;
        }

        private void SetFirstPerson(bool enabled)
        {
            firstPerson = enabled;
            if (bodyRenderers != null)
                foreach (Renderer part in bodyRenderers)
                    if (part != null) part.enabled = !enabled;
            if (firstPersonHandRenderers != null)
                foreach (Renderer part in firstPersonHandRenderers)
                    if (part != null) part.enabled = enabled;
            if (firstPersonRig != null) firstPersonRig.SetVisible(enabled);
            if (proceduralAnimator != null)
                proceduralAnimator.SetThirdPersonVisible(!enabled);
            UpdateCamera();
        }

        private void UpdateCamera()
        {
            if (viewCamera == null || body == null) return;

            Quaternion aim = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 head = transform.position + Vector3.up * 1.76f;
            Vector3 desired = firstPerson
                ? head + aim * Vector3.forward * 0.12f
                : head - aim * Vector3.forward * 5.2f + Vector3.up * 0.8f;

            if (firstPerson && wallClimbing && wallNormal.sqrMagnitude > 0.5f)
                desired += wallNormal.normalized * 0.32f;

            if (!firstPerson)
            {
                Vector3 travel = desired - head;
                int hitCount = Physics.SphereCastNonAlloc(head, 0.25f, travel.normalized,
                    cameraHits, travel.magnitude, ~0, QueryTriggerInteraction.Ignore);
                float nearest = travel.magnitude;
                for (int i = 0; i < hitCount; i++)
                {
                    RaycastHit hit = cameraHits[i];
                    if (hit.collider.GetComponentInParent<SpiderController>() == this) continue;
                    if (hit.distance < nearest) nearest = hit.distance;
                }
                desired = head + travel.normalized * Mathf.Max(0.35f, nearest - 0.12f);
            }

            viewCamera.transform.SetPositionAndRotation(desired, aim);
        }

        private void Respawn()
        {
            roofVaulting = false;
            wallYankDepartureSurface = null;
            body.isKinematic = false;
            LeaveWall();
            wallDetachUntil = Time.time + 0.5f;
            if (leftWeb.attached || leftWeb.firing) ReleaseWeb(leftWeb, true, true);
            if (rightWeb.attached || rightWeb.firing) ReleaseWeb(rightWeb, false, true);
            body.position = new Vector3(0f, 0.08f, -36f);
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            yaw = 0f;
            pitch = 0f;
            lastGroundJumpTime = -10f;
        }

        private void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnGUI()
        {
            if (hudStyle == null)
            {
                hudStyle = new GUIStyle(GUI.skin.label);
                hudStyle.fontSize = 15;
                hudStyle.normal.textColor = Color.white;
                crosshairStyle = new GUIStyle(hudStyle);
                crosshairStyle.alignment = TextAnchor.MiddleCenter;
                crosshairStyle.fontSize = 24;
            }

            GUI.Box(new Rect(14, 14, 455, 113), GUIContent.none);
            GUI.Label(new Rect(26, 22, 440, 24), "WASD Move  |  Mouse Look  |  Space Jump/Leave Wall", hudStyle);
            GUI.Label(new Rect(26, 46, 440, 24), "W at wall: Climb  |  V: View  |  R: Reset", hudStyle);
            GUI.Label(new Rect(26, 70, 440, 24), "Left Click: Shoot / Release / Swap  |  Right Click: Yank", hudStyle);
            GUI.Label(new Rect(26, 94, 440, 24),
                "Webs: " + WebLabel(leftWeb, "LEFT") + " / " +
                WebLabel(rightWeb, "RIGHT") +
                "   |   " + (roofVaulting ? "ROOF JUMP" : wallAtLedge ? "LEDGE: SPACE" :
                    wallClimbing ? "CLIMBING" : firstPerson ? "1st person" : "3rd person"), hudStyle);
            GUI.Label(new Rect(Screen.width / 2f - 15f, Screen.height / 2f - 16f, 30f, 32f),
                "+", crosshairStyle);
            if (Cursor.lockState != CursorLockMode.Locked)
                GUI.Label(new Rect(Screen.width / 2f - 150f, Screen.height / 2f + 32f, 300f, 30f),
                    "Click to capture the mouse", crosshairStyle);
        }

        private string WebLabel(WebState web, string side)
        {
            return side + (web.firing ? " FLYING" : web.attached ?
                web == yankWeb ? " YANK" : " SWING" : " OFF");
        }
    }
}
