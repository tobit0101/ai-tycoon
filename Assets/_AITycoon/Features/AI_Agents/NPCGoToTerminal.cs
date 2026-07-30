using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using AITycoon.Features.Interactables;

namespace AITycoon.Features.AI_Agents
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class NPCGoToTerminal : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Das Terminal zu dem der NPC laufen soll.")]
        public ComputerTerminal targetTerminal;

        [Tooltip("Optionaler Standpunkt vor dem Terminal. Falls leer, wird Terminal-Position verwendet.")]
        public Transform interactPoint;

        [Header("Settings")]
        [Tooltip("Abstand zum Terminal ab dem Interact() ausgelöst wird.")]
        public float interactDistance = 1.5f;

        [Tooltip("Sekunden warten bevor der NPC losläuft (für Tests nützlich).")]
        public float startDelay = 1f;

        private NavMeshAgent agent;
        private NPCWander wander;
        private Animator animator;
        private bool interactTriggered = false;

        private void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            wander = GetComponent<NPCWander>();
            animator = GetComponent<Animator>();

            agent.stoppingDistance = interactDistance;

            if (targetTerminal != null)
                StartCoroutine(GoToTerminal());
        }

        private IEnumerator GoToTerminal()
        {
            yield return new WaitForSeconds(startDelay);

            if (wander != null)
                wander.enabled = false;

            agent.stoppingDistance = interactDistance;

            Vector3 destination = interactPoint != null ? interactPoint.position : targetTerminal.transform.position;
            agent.isStopped = false;
            agent.SetDestination(destination);

            yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

            agent.isStopped = true;
            if (animator != null)
                animator.SetFloat("Speed", 0f);

            // Zum PC drehen
            Vector3 dir = (targetTerminal.transform.position - transform.position);
            dir.y = 0f;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(dir);

            interactTriggered = true;
            targetTerminal.Interact();
        }

        // Kann von außen aufgerufen werden um den Ablauf neu zu starten
        public void TriggerGoToTerminal()
        {
            if (targetTerminal == null) return;
            interactTriggered = false;
            StopAllCoroutines();
            StartCoroutine(GoToTerminal());
        }
    }
}
