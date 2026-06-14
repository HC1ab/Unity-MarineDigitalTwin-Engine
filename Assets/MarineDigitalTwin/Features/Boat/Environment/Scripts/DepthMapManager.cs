using UnityEngine;

namespace MarineDigitalTwin.Environment
{
    public class DepthMapManager : MonoBehaviour
    {
        [SerializeField] private BathymetryClient bathymetryClient;
        [SerializeField] private Transform boat;
        [SerializeField, Min(0.1f)] private float statusUpdateSeconds = 0.5f;
        [SerializeField] private bool logStatus = true;

        private BathymetryData bathymetry;
        private float nextStatusUpdateTime;
        private bool hasOutsideState;
        private bool wasOutside;

        public bool HasBathymetry => bathymetry?.depthPoints != null &&
                                     bathymetry.depthPoints.Length > 0;
        public float CurrentChartDepth { get; private set; } = float.NaN;
        public float CurrentTideLevelMeter => HasBathymetry
            ? bathymetry.tideLevelMeter
            : float.NaN;
        public float CurrentEffectiveDepth { get; private set; } = float.NaN;
        public float SimulationRadius => HasBathymetry
            ? bathymetry.radiusMeters
            : 0f;
        public bool IsOutsideSimulationArea { get; private set; }

        private void Update()
        {
            if (boat == null || !HasBathymetry || Time.unscaledTime < nextStatusUpdateTime)
                return;

            nextStatusUpdateTime = Time.unscaledTime + statusUpdateSeconds;
            UpdateBoatDepthStatus();
        }

        // Can be bound directly to a UI Button for manual refresh.
        public void LoadBathymetry()
        {
            if (bathymetryClient == null)
            {
                Debug.LogWarning("[Diag][Bathymetry.MissingClient]");
                return;
            }

            bathymetryClient.RefreshBathymetry();
        }

        public void SetBathymetry(BathymetryData data)
        {
            if (data?.depthPoints == null || data.depthPoints.Length == 0)
                return;

            bathymetry = data;
            nextStatusUpdateTime = 0f;
            if (boat != null)
                UpdateBoatDepthStatus();
        }

        public float GetNearestDepth(Vector3 worldPosition)
        {
            DepthPoint nearest = FindNearestPoint(worldPosition);
            return nearest != null ? nearest.depth : float.NaN;
        }

        public float GetEffectiveDepth(Vector3 worldPosition)
        {
            float chartDepth = GetNearestDepth(worldPosition);
            return float.IsFinite(chartDepth)
                ? chartDepth + CurrentTideLevelMeter
                : float.NaN;
        }

        public bool IsInsideSimulationRadius(Vector3 worldPosition)
        {
            if (!HasBathymetry)
                return false;

            float distanceSquared =
                worldPosition.x * worldPosition.x + worldPosition.z * worldPosition.z;
            return distanceSquared <= bathymetry.radiusMeters * bathymetry.radiusMeters;
        }

        private DepthPoint FindNearestPoint(Vector3 worldPosition)
        {
            if (!HasBathymetry)
                return null;

            DepthPoint nearest = null;
            float nearestDistanceSquared = float.PositiveInfinity;
            foreach (DepthPoint point in bathymetry.depthPoints)
            {
                float dx = worldPosition.x - point.x;
                float dz = worldPosition.z - point.z;
                float distanceSquared = dx * dx + dz * dz;
                if (distanceSquared >= nearestDistanceSquared)
                    continue;

                nearestDistanceSquared = distanceSquared;
                nearest = point;
            }

            return nearest;
        }

        private void UpdateBoatDepthStatus()
        {
            Vector3 position = boat.position;
            CurrentChartDepth = GetNearestDepth(position);
            CurrentEffectiveDepth = float.IsFinite(CurrentChartDepth)
                ? CurrentChartDepth + CurrentTideLevelMeter
                : float.NaN;
            IsOutsideSimulationArea = !IsInsideSimulationRadius(position);

            if (!hasOutsideState || IsOutsideSimulationArea != wasOutside)
            {
                string message =
                    $"[Diag][Bathymetry.SimulationArea] outside={IsOutsideSimulationArea} " +
                    $"boatXZ=({position.x:F1}, {position.z:F1}) " +
                    $"radius={SimulationRadius:F1}m";
                if (IsOutsideSimulationArea)
                    Debug.LogWarning(message);
                else
                    Debug.Log(message);

                hasOutsideState = true;
                wasOutside = IsOutsideSimulationArea;
            }

            if (logStatus)
            {
                Debug.Log(
                    $"[Diag][Bathymetry.BoatDepth] chartDepth={CurrentChartDepth:F3}m " +
                    $"tideLevelMeter={CurrentTideLevelMeter:F3}m " +
                    $"effectiveDepth={CurrentEffectiveDepth:F3}m " +
                    $"simulationRadius={SimulationRadius:F1}m " +
                    $"isOutsideSimulationArea={IsOutsideSimulationArea}"
                );
            }
        }
    }
}
