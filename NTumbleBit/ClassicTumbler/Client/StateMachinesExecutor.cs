using Microsoft.Extensions.Logging;
using NBitcoin;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler.Client
{
    public class StateMachinesExecutor : TumblerServiceBase
    {
        protected HashSet<CycleParameters> ManagedCycles = new HashSet<CycleParameters>();
        public bool IsTumbling { get; set; }

        public StateMachinesExecutor(
            TumblerClientRuntime runtime)
        {
            Runtime = runtime ?? throw new ArgumentNullException("runtime");
            _ParametersHash = Runtime.TumblerParameters.GetHash();
        }

        uint160 _ParametersHash;
        public TumblerClientRuntime Runtime
        {
            get; set;
        }

        public override string Name => "mixer";

        public int InvalidPhaseCount
        {
            get;
            private set;
        }

        protected override void StartCore(CancellationToken cancellationToken)
        {
            IsTumbling = true;
            new Thread(() =>
            {
                Logs.Client.LogInformation("State machines started");
                uint256 lastBlock = uint256.Zero;
                int lastCycle = 0;
                while(true)
                {
                    try
                    {
                        lastBlock = Runtime.Services.BlockExplorerService.WaitBlock(lastBlock, cancellationToken);
                        var height = Runtime.Services.BlockExplorerService.GetCurrentHeight();
                        Logs.Client.LogInformation("New Block: " + height);
                        var cycle = Runtime.TumblerParameters.CycleGenerator.GetRegisteringCycle(height);
                        if (lastCycle != cycle.Start)
                        {
                            // Only start a new cycle if there are sufficient wallet funds
                            bool firstCycle = ManagedCycles.All(c => c.IsPostRegistration(height));
                            if (Runtime.HasEnoughFundsForCycle(firstCycle))
                            {
                                lastCycle = cycle.Start;
                                ManagedCycles.Add(cycle);

                                Logs.Client.LogInformation("New Cycle: {0}", cycle.Start);
                                PaymentStateMachine.State state = GetPaymentStateMachineState(cycle);
                                if (state == null)
                                {
                                    var stateMachine = new PaymentStateMachine(Runtime, null);
                                    stateMachine.NeedSave = true;
                                    Save(stateMachine, cycle.Start);
                                }
                            } else
                            {
                                Logs.Client.LogInformation("There are not enough funds to sustain another tumbling cycle.");
                            }
                        }

                        var progressInfo = new ProgressInfo(height, Runtime.DataDir);

                        var cycles = Runtime.TumblerParameters.CycleGenerator.GetCycles(height);
                        var machineStates = cycles
                                                .SelectMany(c => Runtime.Repository.List<PaymentStateMachine.State>(GetPartitionKey(c.Start)))
                                                .Where(m => m.TumblerParametersHash == _ParametersHash)
                                                .ToArray();
                        NBitcoin.Utils.Shuffle(machineStates);
                        bool hadInvalidPhase = false;

                        if (Runtime.Network != Network.RegTest)
                        {
                            //Waiting for the block to propagate to server so invalid-phase happens less often
                            //This also make the server less overwhelmed by sudden request peak.
                            var waitRandom = TimeSpan.FromSeconds(RandomUtils.GetUInt32() % 120 + 10);
                            Logs.Client.LogDebug("Waiting {0} seconds before updating machine states...", (int)waitRandom.TotalSeconds);

                            cancellationToken.WaitHandle.WaitOne(waitRandom);
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                        else
                        {
                            // Need to ensure that the rest of the processing only happens after the server
                            // has definitely recognised that a block has been received. Invalid phase
                            // errors will result otherwise.
                            Logs.Client.LogDebug("Waiting 2 seconds before updating machine states...");

                            cancellationToken.WaitHandle.WaitOne(2);
                            cancellationToken.ThrowIfCancellationRequested();
                        }

                        foreach (var state in machineStates)
                        {
                            var machine = new PaymentStateMachine(Runtime, state);
                            if(machine.Status == PaymentStateMachineStatus.Wasted)
                            {
                                Logs.Client.LogDebug($"Skipping cycle {machine.StartCycle}, because it is wasted");
                                continue;
                            }

                            var statusBefore = machine.GetInternalState();
                            try
                            {
                                var cycleProgressInfo = machine.Update();
                                InvalidPhaseCount = 0;
                                if (cycleProgressInfo != null)
                                    progressInfo.CycleProgressInfoList.Add(cycleProgressInfo);
                            }
                            catch (PrematureRequestException)
                            {
                                Logs.Client.LogInformation("Skipping update, need to wait for tor circuit renewal for cycle {0} ({1})", machine.StartCycle, machine.Status);
                                break;
                            }
                            catch(Exception ex) when(IsInvalidPhase(ex))
                            {
                                if(!hadInvalidPhase)
                                {
                                    hadInvalidPhase = true;
                                    InvalidPhaseCount++;
                                    if(InvalidPhaseCount > 2)
                                    {
                                        Logs.Client.LogError(new EventId(), ex, "Invalid-Phase happened repeatedly, check that your node currently " +
                                            "at height {0} is currently sync to the network; cycle {1} ({2})", height, machine.StartCycle, machine.Status);
                                    }
                                }
                            }
                            catch(Exception ex)
                            {
                                Logs.Client.LogError(new EventId(), ex, "Unhandled StateMachine Error for cycle {0} ({1})", machine.StartCycle, machine.Status);
                            }

                            Save(machine);
                        }
                        Logs.Client.LogDebug("Number of running cycles reported to the progress API: {0}", progressInfo.CycleProgressInfoList.Count);

                        int numberOfCompletedCycles = ManagedCycles.Count(c => c.IsComplete(height));
                        bool allCyclesAreComplete = !ManagedCycles.Any() || (numberOfCompletedCycles == ManagedCycles.Count);
                        if (allCyclesAreComplete)
                        {
                            Logs.Client.LogInformation("Tumbling finished; all {0} tumbling cycles have been completed", ManagedCycles.Count);
                            Stop();
                            break;
                        }
                        else
                        {
                            Logs.Client.LogDebug("Tumbling running; there are {0} of {1} cycles completed", numberOfCompletedCycles, ManagedCycles.Count);
                        }

                        progressInfo.Save();
                    }
                    catch (OperationCanceledException) when(cancellationToken.IsCancellationRequested)
                    {
                        Logs.Client.LogInformation("Tumbling cancelled; all {0} tumbling cycles have been aborted", ManagedCycles.Count);
                        Stop();
                        break;
                    }
                    catch(Exception ex)
                    {
                        Logs.Client.LogError(new EventId(), ex, "StateMachineExecutor Error: " + ex.ToString());
                        cancellationToken.WaitHandle.WaitOne(5000); 
                    }
                }
                IsTumbling = false;
            }).Start();
        }



        private bool IsInvalidPhase(Exception ex)
        {
            return ex.Message.IndexOf("invalid-phase", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public PaymentStateMachine.State GetPaymentStateMachineState(CycleParameters cycle)
        {
            var state = Runtime.Repository.Get<PaymentStateMachine.State>(GetPartitionKey(cycle.Start), "");
            if(state == null)
                return null;
            return state.TumblerParametersHash == _ParametersHash ? state : null;
        }

        private string GetPartitionKey(int cycle)
        {
            return "Cycle_" + cycle;
        }

        private void Save(PaymentStateMachine stateMachine, int? cycleStart = null)
        {
            if(stateMachine.NeedSave)
                Runtime.Repository.UpdateOrInsert(GetPartitionKey(cycleStart ?? stateMachine.StartCycle), "", stateMachine.GetInternalState(), (o, n) => n);
        }
    }
}
