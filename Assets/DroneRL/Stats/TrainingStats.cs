using Unity.MLAgents;
using UnityEngine;

public static class TrainingStats
{
    public static void Record(string key, float value, StatAggregationMethod method = StatAggregationMethod.Average)
    {
        Academy.Instance.StatsRecorder.Add(key, value, method);
    }
}
