using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;


namespace CivilFX
{
    [RequireComponent(typeof(Camera))]
    public class BareCameraController : MonoBehaviour
    {
        public class CameraSetting
        {
            public class ClippingPlanes
            {
                public float near;
                public float far;
            }

            public float fieldOfView;
            public ClippingPlanes clippingPlanes = new ClippingPlanes();
            public Vector3 rotation;
            public Vector3 position;

        }

        public enum CameraState
        {
            Default,
            MouseZoomIn,
            MouseZoomOut,
            TouchZoomIn,
            TouchZoomOut,
            MovingForward,
            MovingBackward,
            Reset,
            Stop,
        }

        [SerializeField]
        public Transform[] views;

        public float speed = 0.1f;

        private Transform currentView;
        private int currentIndex = 1;

        [SerializeField]
        private Transform defaultView;

        [SerializeField]
        private float rotationSpeed = 0.1f;

        [SerializeField]
        private float[] clampValues;



        private Transform movingTarget;

        int RETURN_TIME = 300;
        int trackTimer = 0;

        public Vector3 offset;

        private Vector3 velocity = Vector3.zero;

        private bool isFreeRotation;

        private float zAxis;

        private Camera cam;


        private Vector3 lastMousePos;
        CameraState camState = CameraState.Default;
        public bool boundingBox = true;

        [SerializeField]
        private Transform miniMapCamera;

        public Camera previewCamera;

        List<Touch> touchList = new List<Touch>();

        //
        // PRIVATE VARIABLES
        //
        private float turnSpeed = 1.0f;       // Speed of camera turning when mouse moves in along an axis
        private float panSpeed = 0.5f;        // Speed of the camera when being panned
        private float pinchSpeed = 10.0f;     // Speed of the camera going back and forth with Pinch

        private Vector3 panOrigin;       // Position of cursor when mouse dragging starts
        private Vector3 rotateOrigin;    // Position of cursor when mouse dragging starts
        private Vector3 zoomOrigin;      // Position of cursor when mouse dragging starts
        private float pinchOrigin;       // Original Distance between the two touches

        private Vector3 panLastPos;      // Position of cursor when mouse dragging starts
        private Vector3 rotateLastPos;   // Position of cursor when mouse dragging starts
        private Vector3 zoomLastPos;     // Position of cursor when mouse dragging starts

        private bool isPanning;     // Is the camera being panned?
        private bool isRotating;    // Is the camera being rotated?
        private bool isZooming;     // Is the camera zooming?

        private float mouseZoomSpeed = 50.0f;

        private Transform cameraTransform;
        private bool deviceInverted = false;
        private bool invertForTouch = false;

        private Vector3 positionOffset;

        private bool isDoneRotation;
        private bool isDoneMoving;

        private Vector3 lastMouseDownPos;
        private float lastTouchTime;

        CameraSetting defaultSetting;

        //callbacks
        public delegate void UnHookCallBack();
        public delegate void HookCallBack();

        private bool isMouseOverUI;

        UnHookCallBack unhookCallback;
        HookCallBack hookCallBack;
        UnHookCallBack userControlledCallback;

        int adjustRotationSpeed = 0;

        private bool flag;

        private void Awake()
        {
            cam = GetComponent<Camera>();

            defaultSetting = new CameraSetting();
            defaultSetting.fieldOfView = cam.fieldOfView;
            defaultSetting.clippingPlanes.near = cam.nearClipPlane;
            defaultSetting.clippingPlanes.far = cam.farClipPlane;
            boundingBox = true;
        }

        // Use this for initialization
        void Start()
        {
            if (defaultView != null)
            {
                HookView(defaultView);
            }
            isFreeRotation = false;
            cameraTransform = gameObject.transform;

            adjustRotationSpeed = 1;
            Input.simulateMouseWithTouches = false;
            positionOffset = offset;
        }


        private void Update()
        {
            flag = false;

            if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1) && touchList.Count == 0)
            {
                isMouseOverUI = false;
            }

            if (/*(EventSystem.current.IsPointerOverGameObject() || */ IsPointerOverUIObject())
            {
                isMouseOverUI = true;
            }


            GatherTouches();
            if (isMouseOverUI)
            {
                return;
            }

            ProcessMouseInput();
            ProcessMobileInput();

            if (flag)
            {
                userControlledCallback?.Invoke();
            }


            //clamping camera position
            if (boundingBox && movingTarget == null && clampValues != null && clampValues.Length == 6)
            {
                Vector3 pos = cameraTransform.position;
                cameraTransform.position = new Vector3(Mathf.Clamp(pos.x, clampValues[0], clampValues[1]), Mathf.Clamp(pos.y, clampValues[2], clampValues[3]), Mathf.Clamp(pos.z, clampValues[4], clampValues[5]));
            }

        }

        private void ProcessMouseInput()
        {



            if (Input.GetMouseButtonDown(0))
            {
                camState = CameraState.MouseZoomOut;
            }
            if (Input.GetMouseButtonDown(1))
            {
                camState = CameraState.MouseZoomIn;
            }

            if (Input.GetMouseButton(0) && Input.GetMouseButton(1))
            {
                Vector3 zoom = Vector3.zero;
                if (camState == CameraState.MouseZoomIn)
                {
                    zoom = cameraTransform.forward;
                }
                else if (camState == CameraState.MouseZoomOut)
                {
                    zoom = -cameraTransform.forward;

                }
                cameraTransform.Translate(zoom * Time.deltaTime * mouseZoomSpeed, Space.World);
                flag = true;
            }
            else
            {
                camState = CameraState.Default;
            }
            //left mouse drag to pan
            if (Input.GetMouseButtonDown(0))
            {
                //Unhook both moving and rotating
                UnHookView(true);
                lastMouseDownPos = Input.mousePosition;

            }

            if (Input.GetMouseButton(0))
            {
                //left mouse drag
                Vector3 currentMousePos = Input.mousePosition;
                Vector3 panPos = currentMousePos - lastMouseDownPos;

                if (panPos == Vector3.zero)
                {
                    return;
                }

                Vector3 pan = new Vector3(panPos.x * panSpeed * Time.deltaTime, panPos.y * panSpeed * Time.deltaTime, 0);
                cameraTransform.Translate(pan, Space.Self);
                flag = true;
            }


            //right mouse drag to rotate
            if (Input.GetMouseButtonDown(1))
            {
                //unhook rotation
                UnHookView(false);

                zAxis = cameraTransform.rotation.eulerAngles.z;
            }

            if (Input.GetMouseButton(1))
            {
                //Rote camera
                transform.Rotate(new Vector3(-Input.GetAxis("Mouse Y") * (rotationSpeed + 20f) * Time.deltaTime, Input.GetAxis("Mouse X") * (turnSpeed / 10.0f + 20f) * Time.deltaTime, zAxis));
                float X = transform.rotation.eulerAngles.x;
                float Y = transform.rotation.eulerAngles.y;
                transform.rotation = Quaternion.Euler(X, Y, zAxis);
            }


            //Scroll wheel to zoom
            float scrollValue = Input.GetAxis("Mouse ScrollWheel");

            Vector3 pinch = Vector3.zero;
            if (scrollValue > 0)
            {
                //scroll up
                pinch = cameraTransform.forward * 5f;

            }
            else if (scrollValue < 0)
            {
                //scroll down
                pinch = -cameraTransform.forward * 5f;
            }
            cameraTransform.Translate(pinch, Space.World);




        }


        private void GatherTouches()
        {
            touchList.Clear();
            touchList.Clear();

            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                    AddTouchToListIfActive(Input.GetTouch(i));
            }
        }

        private void ProcessMobileInput()
        {
            if (touchList.Count == 0)
            {
                return;
            }

           
            if (touchList.Count == 1)
            {
                if (isRotating)
                {
                    //unhook rotation
                    UnHookView(false);


                    rotateLastPos = touchList[0].position;

                    // Rotate Camera
                    Vector3 newPos = Camera.main.ScreenToViewportPoint(rotateLastPos - rotateOrigin);
                    if (invertForTouch) newPos = newPos * -1f;
                    cameraTransform.RotateAround(cameraTransform.position, cameraTransform.right, -newPos.y * turnSpeed);
                    cameraTransform.RotateAround(cameraTransform.position, Vector3.up, newPos.x * turnSpeed);
                }

                if (touchList[0].phase == TouchPhase.Ended)
                {
                    float currentTime = Time.time;
                    if (currentTime - lastTouchTime < 0.3f)
                    {
                        Debug.Log("DoubleTap");

                        RaycastHit hit = new RaycastHit();
                        Ray ray = cam.ScreenPointToRay(touchList[0].position);

                        if (Physics.Raycast(ray, out hit))
                        {
                            hit.transform.gameObject.SendMessage("OnMouseUp", SendMessageOptions.DontRequireReceiver);
                        }
                    }
                    lastTouchTime = currentTime;
                }
            }

            if (touchList.Count >= 2)
            {
                //Unhook both moving and rotating
                UnHookView(true);


                float pinchSeperation = (touchList[0].position - touchList[1].position).magnitude;
                float ratio = (Mathf.Approximately(pinchOrigin, float.Epsilon) ? 1.0f : pinchSeperation / pinchOrigin) - 1.0f; //Mathf.Sign(pinchDelta * (pinchDelta.magnitude / size.magnitude);

                // Move the camera linearly along Z axis
                Vector3 pinch = (1f * ratio) * pinchSpeed * cameraTransform.forward;
                cameraTransform.Translate(pinch, Space.World);


                //Pan
                Vector3 touchPanOffset = (touchList[0].position + touchList[1].position) * 0.5f;
                panLastPos = touchPanOffset;
                Vector3 panPos = (panLastPos - panOrigin) * 0.01f;
                if (invertForTouch) panPos = panPos * -1f;
                Vector3 pan = new Vector3(panPos.x * panSpeed, panPos.y * panSpeed, 0);
                cameraTransform.Translate(pan, Space.Self);
            }


        }


        private void AddTouchToListIfActive(Touch touch)
        {
            touchList.Add(touch);

            if (touch.phase == TouchPhase.Began)
            {
                Vector2 pos = touch.position;
                rotateOrigin = (Vector3)pos;

                if (touchList.Count == 2)
                {
                    pinchOrigin = (touchList[0].position - touchList[1].position).magnitude;
                    panOrigin = (touchList[0].position + touchList[1].position) * 0.5f;
                }

                isRotating = true;
            }
        }

        private bool IsPointerOverUIObject()
        {
            if (EventSystem.current == null)
            {
                return false;
            }
            PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
            eventDataCurrentPosition.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventDataCurrentPosition, results);


            //consume this event
            EventSystem.current = null;


            return results.Count > 0;
        }



        private void FixedUpdate()
        {
            if (currentView == null || currentView == cameraTransform)
            {
                return;
            }

            //Debug.Log("Moving camera");
            Vector3 newPos = currentView.position + positionOffset;

            //Moving camera
            if (!isDoneMoving)
            {
                Vector3 pos = Vector3.SmoothDamp(cameraTransform.position, newPos, ref velocity, 0.5f);
                //Debug.Log("View position: " + newPos);
                //Debug.Log("Position " + pos);
                cameraTransform.position = pos;
                //float distance = Vector3.Distance(pos, newPos);
                //Debug.Log("Distance: " + distance);
                /*
                if (Vector3.Distance(pos, newPos) <= 0.0006f)
                { 
                    //disable for now
                    //isDoneMoving = true;
                }
                */
            }

            //Rotating camera
            //10/17/2018: added isDoneRotation to prevent rotation at still camera
            //remove it if it gives you rotating issue.
            if (!isFreeRotation && !isDoneRotation)
            {
                Quaternion rot = Quaternion.Slerp(cameraTransform.rotation, currentView.rotation, (3.0f / adjustRotationSpeed) * Time.fixedDeltaTime);
                cameraTransform.rotation = rot;
                if (rot == currentView.rotation && movingTarget == null)
                {
                    isDoneRotation = true;
                }
            }
            //transform.LookAt(currentView);
        }

        private void LateUpdate()
        {
            if (miniMapCamera != null)
            {
                Vector3 pos = cameraTransform.position;
                pos.y = 10000;
                miniMapCamera.transform.position = pos;
            }
        }


        public void HookView()
        {
            if (movingTarget != null)
            {
                HookView(defaultView);
            }

        }

        public void HookView(int index)
        {
            Debug.Log("Hooking Views at: " + index);
            if (index >= 0 && index < views.Length)
            {
                HookView(views[index]);
            }
            movingTarget = null;
        }

        public void HookView(Transform trans, bool isMoving = false, bool isActive = true)
        {
            //call unhook to handle cleanning stuffs
            UnHookView();

            if (isMoving)
            {
                movingTarget = trans;
                hookCallBack?.Invoke();
            }
            else
            {
                movingTarget = null;
            }

            if (isActive)
            {
                currentView = trans;
            }


            //lock camera rotation
            isFreeRotation = false;
            isDoneRotation = false;
            isDoneMoving = false;

            adjustRotationSpeed = 1;

            //set UI text
            //UITextManager.SetText("CameraNameUIText", currentView.gameObject.name);

            
        }

        //UnHook a view to camera
        //hard == true : unhook completely (both moving and rotating)
        //hard == false : unhook only rotation
        public void UnHookView(bool hard = true)
        {
            if (hard)
            {
                currentView = null;
                unhookCallback?.Invoke();
                movingTarget = null;
                //UITextManager.SetText("CameraNameUIText", "Free Camera");
            }
            isFreeRotation = !hard;

            /*
            cam.fieldOfView = defaultSetting.fieldOfView;
            cam.nearClipPlane = defaultSetting.clippingPlanes.near;
            cam.farClipPlane = defaultSetting.clippingPlanes.far;
            */

        }

        public void SetTurnSpeed(float f)
        {
            turnSpeed = f;

        }

        public void SetPositionOffset(Vector3 v3)
        {
            positionOffset = v3;
        }

        public void SetFlyThroughSpeed(float f)
        {
            panSpeed = f;
            pinchSpeed = 10.0f + f;
        }


        public void OnUnHook(UnHookCallBack unhook)
        {
            unhookCallback += unhook;
        }

        public void OnHook(HookCallBack hook)
        {
            hookCallBack += hook;
        }

        public void OnUserControlled(UnHookCallBack cb)
        {
            userControlledCallback += cb;
        }

        public void SetClippingPlanes(float[] f)
        {
            cam.nearClipPlane = f[0];
            cam.farClipPlane = f[1];
        }

        public void SetFieldOfView(float f)
        {
            cam.fieldOfView = f;
        }

        
        public void AdjustCameraSpeed(int v)
        {
            adjustRotationSpeed = v;

            /*
            if (movingTarget != null)
            {
                movingTarget.gameObject.GetComponent<MoveDummyCamera>().AdjustSpeed(v);
            }
            */
        }

        public void RestartOnOriginalPath()
        {
            /*
            if (movingTarget != null)
            {
                movingTarget.gameObject.GetComponent<MoveDummyCamera>().Restart();
            }
            */
        }
        
    }
}