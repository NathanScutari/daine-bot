using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaineBot.Services
{
    public class BotReadyService
    {
        private readonly TaskCompletionSource<bool> _readyTcs = new();

        public Task Ready => _readyTcs.Task;

        public void MarkReady() => _readyTcs.TrySetResult(true);
    }
}
