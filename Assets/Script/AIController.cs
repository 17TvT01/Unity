using UnityEngine;
using System.Collections;

public class AIController : MonoBehaviour
{
    private GameObject forcedTarget;
    private float forceTargetTimeRemaining;

    public void ForceTarget(GameObject target, float duration)
    {
        forcedTarget = target;
        forceTargetTimeRemaining = duration;
        StartCoroutine(ClearForcedTargetAfterDelay());
    }

    public GameObject GetCurrentTarget()
    {
        if (forcedTarget != null && forceTargetTimeRemaining > 0)
        {
            return forcedTarget;
        }

        // Default targeting logic here
        return null;
    }

    private IEnumerator ClearForcedTargetAfterDelay()
    {
        yield return new WaitForSeconds(forceTargetTimeRemaining);
        forcedTarget = null;
        forceTargetTimeRemaining = 0;
    }

    private void Update()
    {
        if (forceTargetTimeRemaining > 0)
        {
            forceTargetTimeRemaining -= Time.deltaTime;
        }
    }
}
