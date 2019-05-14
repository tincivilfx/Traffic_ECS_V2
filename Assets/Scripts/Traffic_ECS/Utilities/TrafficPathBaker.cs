using UnityEngine;

using System.Collections.Generic;
using System.Collections;

namespace CivilFX.TrafficECS
{

    public class TrafficPathBaker : MonoBehaviour
    {

        [SerializeField]
        private int pathCount;

        [Header("Resolution for all paths")]
        [Tooltip("This value will override paths' MPH at run time (it is not saved)")]
        public bool useOneResolutionForAll;
        [Range(1, 100)]
        public int resolution;

        public string savedLocation = "Assets";
        public List<TrafficPath> paths;

        public bool doBakeWhenPlaying;
        public bool exitPlayMode;

        public void Awake()
        {
#if UNITY_EDITOR
            if (doBakeWhenPlaying)
            {
                StartBaking();
            }
#endif
        }



        public void StartBaking()
        {
            gameObject.name = gameObject.name + "Status: In Progress";

            Debug.Log("Start Baking...");
            StartCoroutine(WatchDog());
        }

        IEnumerator WatchDog()
        {
            List<Coroutine> workers = new List<Coroutine>(paths.Count);
            foreach (var path in paths)
            {

                if (path != null)
                {
                    if (useOneResolutionForAll)
                    {
                        path.bakedResolution = resolution;
                    }
                    workers.Add(StartCoroutine(Worker(path)));
                }
            }

            foreach (var worker in workers)
            {
                yield return worker;
            }

            //Done baking
            Debug.Log("Baking is done");
            gameObject.name = gameObject.name + "Status: Done";

#if UNITY_EDITOR
            if (exitPlayMode && Application.isPlaying)
            {
                UnityEditor.EditorApplication.ExecuteMenuItem("Edit/Play");
            }
#endif
        }


        //Worker thread to do the baking
        IEnumerator Worker(TrafficPath path)
        {
            Debug.Log("Start Worker");
            //open folder to create new file
            //System.IO.FileStream fstream = Ultilities.OpenFile(path.gameObject.name, System.IO.FileMode.OpenOrCreate);

            string workerName = "Worker: " + path.gameObject.name;

            GameObject trafficBaker = new GameObject(workerName);
            trafficBaker.transform.SetParent(transform);

            float progress = 0;
            GoSpline spline = path.Spline();
            float speed = path.bakedResolution;
            float duration = path.Spline().pathLength;

            List<Vector3> pathNodes = new List<Vector3>();
            string pathName = path.gameObject.name;

            //baking
            while (progress <= 1.0f)
            {
                yield return new WaitForFixedUpdate();

                progress += Time.fixedDeltaTime / (duration / (speed * 0.44704f));
                Vector3 pos = spline.getPointOnPath(progress);
                trafficBaker.transform.position = pos;
                pathNodes.Add(pos);
                trafficBaker.name = workerName + ": " + progress * 100 + "%";

            }
            //done baking

            Debug.Log(workerName + ": Done");
            Debug.Log("Nodes Count: " + pathNodes.Count);
            /*
            BakedTrafficPath bakedPath = ScriptableObject.CreateInstance<BakedTrafficPath>();
            bakedPath.Init(pathNodes, pathName, path.pathType, path.pathSpeedMPH, path.bakedResolution, path.splitChance, path.smartTraffic, path.phaseTypes, path.notes);
            bakedPath.CreateAndSave(savedLocation);
            */
            yield return null;

        }

    }



}