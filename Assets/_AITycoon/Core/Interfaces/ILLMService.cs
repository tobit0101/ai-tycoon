using UnityEngine;

namespace AITycoon.Core.Interfaces
{
    public interface ILLMService
    {
        Awaitable<string> EnqueueRequestAsync(string prompt);
    }
}