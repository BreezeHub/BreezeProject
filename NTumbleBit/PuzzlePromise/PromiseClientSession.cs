﻿using NBitcoin;
using NTumbleBit.BouncyCastle.Math;
using NBitcoin.Crypto;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NTumbleBit.PuzzlePromise
{
    public enum PromiseClientStates
    {
        WaitingEscrow,
        WaitingSignatureRequest,
        WaitingCommitments,
        WaitingCommitmentsProof,
        Completed
    }

    public class PromiseClientSession : EscrowReceiver
    {
        private abstract class HashBase
        {
            public ServerCommitment Commitment { get; internal set; }
            public abstract uint256 GetHash();
            public int Index { get; set; }
        }

        private class RealHash : HashBase
        {
            public RealHash(Transaction tx, ScriptCoin coin, Network network)
            {
                _BaseTransaction = tx;
                _Escrow = coin;
                _network = network;
            }

            private readonly ScriptCoin _Escrow;
            private readonly Transaction _BaseTransaction;
            private readonly Network _network;

            public Money FeeVariation { get; set; }

            public override uint256 GetHash()
            {
                var escrow = EscrowScriptPubKeyParameters.GetFromCoin(_Escrow);
                var coin = _Escrow.Clone(_network);
                coin.OverrideScriptCode(escrow.GetInitiatorScriptCode());
                return GetTransaction().GetSignatureHash(_network, coin, SigHash.All);
            }

            public Transaction GetTransaction()
            {
                var clone = _BaseTransaction.Clone();
                clone.Outputs[0].Value -= FeeVariation;
                return clone;
            }
        }

        private class FakeHash : HashBase
        {
            public FakeHash(PromiseParameters parameters)
            {
                Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            }

            public uint256 Salt { get; set; }
            public PromiseParameters Parameters { get; private set; }

            public override uint256 GetHash()
            {
                return Parameters.CreateFakeHash(Salt);
            }
        }

        public PromiseClientSession(Network network, PromiseParameters parameters = null)
        {
            _Parameters = parameters ?? new PromiseParameters();
            _network = network;
            InternalState = new State();
        }

        public PromiseParameters Parameters
        {
            get { return _Parameters; }
        }

        public new class State : EscrowReceiver.State
        {
            public Transaction Cashout { get; set; }
            public ServerCommitment[] Commitments { get; set; }
            public uint256[] FakeSalts { get; set; }
            public uint256 IndexSalt { get; set; }
            public Money[] FeeVariations { get; set; }

            public Quotient[] Quotients { get; set; }
            public PromiseClientStates Status { get; set; }
            public int[] FakeIndexes { get; set; }
            public BlindFactor BlindFactor { get; set; }
        }

        private readonly PromiseParameters _Parameters;
        private readonly Network _network;
        private HashBase[] _Hashes;

        public PromiseClientSession(Network network, PromiseParameters parameters, State state) : this(network,
            parameters)
        {
            if (state == null)
                return;
            InternalState = Serializer.Clone(state);
            if (InternalState.Commitments != null)
            {
                _Hashes = new HashBase[InternalState.Commitments.Length];
                int fakeI = 0, realI = 0;
                for (int i = 0; i < _Hashes.Length; i++)
                {
                    HashBase hash = null;
                    if (InternalState.FakeIndexes != null && InternalState.FakeIndexes.Contains(i))
                    {
                        hash = new FakeHash(_Parameters)
                        {
                            Salt = InternalState.FakeSalts[fakeI++]
                        };
                    }
                    else
                    {
                        hash = new RealHash(InternalState.Cashout, InternalState.EscrowedCoin, network)
                        {
                            FeeVariation = InternalState.FeeVariations[realI++]
                        };
                    }

                    hash.Index = i;
                    hash.Commitment = InternalState.Commitments[i];
                    _Hashes[i] = hash;
                }
            }
        }

        public State GetInternalState()
        {
            State state = Serializer.Clone(InternalState);
            state.FakeSalts = null;
            state.FeeVariations = null;
            state.Commitments = null;
            if (_Hashes != null)
            {
                var commitments = new List<ServerCommitment>();
                var fakeSalts = new List<uint256>();
                var feeVariations = new List<Money>();
                for (int i = 0; i < _Hashes.Length; i++)
                {
                    commitments.Add(_Hashes[i].Commitment);
                    if (_Hashes[i] is FakeHash fake)
                    {
                        fakeSalts.Add(fake.Salt);
                    }

                    if (_Hashes[i] is RealHash real)
                    {
                        feeVariations.Add(real.FeeVariation);
                    }
                }

                state.FakeSalts = fakeSalts.ToArray();
                state.FeeVariations = feeVariations.ToArray();
                state.Commitments = commitments.ToArray();
            }

            return state;
        }


        public override void ConfigureEscrowedCoin(ScriptCoin escrowedCoin, Key escrowKey)
        {
            AssertState(PromiseClientStates.WaitingEscrow);
            base.ConfigureEscrowedCoin(escrowedCoin, escrowKey);
            InternalState.Status = PromiseClientStates.WaitingSignatureRequest;
        }

        public SignaturesRequest CreateSignatureRequest(IDestination cashoutDestination, FeeRate feeRate)
        {
            if (cashoutDestination == null)
                throw new ArgumentNullException(nameof(cashoutDestination));
            return CreateSignatureRequest(cashoutDestination.ScriptPubKey, feeRate);
        }

        public SignaturesRequest CreateSignatureRequest(Script cashoutDestination, FeeRate feeRate)
        {
            if (cashoutDestination == null)
                throw new ArgumentNullException(nameof(cashoutDestination));
            if (feeRate == null)
                throw new ArgumentNullException(nameof(feeRate));
            AssertState(PromiseClientStates.WaitingSignatureRequest);

            Transaction cashout = new Transaction();
            cashout.AddInput(new TxIn(InternalState.EscrowedCoin.Outpoint));
            cashout.Inputs[0].ScriptSig = new Script(
                Op.GetPushOp(TrustedBroadcastRequest.PlaceholderSignature),
                Op.GetPushOp(TrustedBroadcastRequest.PlaceholderSignature),
                Op.GetPushOp(InternalState.EscrowedCoin.Redeem.ToBytes())
            );
            cashout.Inputs[0].Witnessify();
            cashout.AddOutput(new TxOut(InternalState.EscrowedCoin.Amount, cashoutDestination));
            cashout.Outputs[0].Value -= feeRate.GetFee(cashout.GetVirtualSize());


            List<HashBase> hashes = new List<HashBase>();
            for (int i = 0; i < Parameters.RealTransactionCount; i++)
            {
                RealHash h = new RealHash(cashout, InternalState.EscrowedCoin, _network);
                h.FeeVariation = Money.Satoshis(i);
                hashes.Add(h);
            }

            for (int i = 0; i < Parameters.FakeTransactionCount; i++)
            {
                FakeHash h = new FakeHash(Parameters);
                h.Salt = new uint256(RandomUtils.GetBytes(32));
                hashes.Add(h);
            }

            _Hashes = hashes.ToArray();
            NBitcoin.Utils.Shuffle(_Hashes, RandomUtils.GetInt32());
            for (int i = 0; i < _Hashes.Length; i++)
            {
                _Hashes[i].Index = i;
            }

            var fakeIndices = _Hashes.OfType<FakeHash>().Select(h => h.Index).ToArray();
            uint256 indexSalt = null;
            var request = new SignaturesRequest
            {
                Hashes = _Hashes.Select(h => h.GetHash()).ToArray(),
                FakeIndexesHash = PromiseUtils.HashIndexes(ref indexSalt, fakeIndices),
            };
            InternalState.IndexSalt = indexSalt;
            InternalState.Cashout = cashout.Clone();
            InternalState.Status = PromiseClientStates.WaitingCommitments;
            InternalState.FakeIndexes = fakeIndices;
            return request;
        }

        public ClientRevelation Reveal(ServerCommitment[] commitments)
        {
            if (commitments == null)
                throw new ArgumentNullException(nameof(commitments));
            if (commitments.Length != Parameters.GetTotalTransactionsCount())
                throw new ArgumentException("Expecting " + Parameters.GetTotalTransactionsCount() + " commitments");
            AssertState(PromiseClientStates.WaitingCommitments);

            List<uint256> salts = new List<uint256>();
            List<int> indexes = new List<int>();
            foreach (var fakeHash in _Hashes.OfType<FakeHash>())
            {
                salts.Add(fakeHash.Salt);
                indexes.Add(fakeHash.Index);
            }

            for (int i = 0; i < commitments.Length; i++)
            {
                _Hashes[i].Commitment = commitments[i];
            }

            InternalState.Status = PromiseClientStates.WaitingCommitmentsProof;
            return new ClientRevelation(indexes.ToArray(), InternalState.IndexSalt, salts.ToArray());
        }

        public PuzzleValue CheckCommitmentProof(ServerCommitmentsProof proof)
        {
            if (proof == null)
                throw new ArgumentNullException(nameof(proof));
            if (proof.FakeSolutions.Length != Parameters.FakeTransactionCount)
                throw new ArgumentException("Expecting " + Parameters.FakeTransactionCount + " solutions");
            if (proof.Quotients.Length != Parameters.RealTransactionCount - 1)
                throw new ArgumentException("Expecting " + (Parameters.RealTransactionCount - 1) + " quotients");
            AssertState(PromiseClientStates.WaitingCommitmentsProof);

            var fakeHashes = _Hashes.OfType<FakeHash>().ToArray();
            for (int i = 0; i < proof.FakeSolutions.Length; i++)
            {
                var fakeHash = fakeHashes[i];
                var solution = proof.FakeSolutions[i];

                if (solution._Value.CompareTo(Parameters.ServerKey._Key.Modulus) >= 0)
                    throw new PuzzleException("Solution bigger than modulus");

                if (!new Puzzle(Parameters.ServerKey, fakeHash.Commitment.Puzzle).Verify(solution))
                    throw new PuzzleException("Invalid puzzle solution");

                if (!IsValidSignature(solution, fakeHash, out ECDSASignature sig))
                    throw new PuzzleException("Invalid ECDSA signature");
            }


            var realHashes = _Hashes.OfType<RealHash>().ToArray();
            for (int i = 1; i < Parameters.RealTransactionCount; i++)
            {
                var q = proof.Quotients[i - 1]._Value;
                var p1 = realHashes[i - 1].Commitment.Puzzle._Value;
                var p2 = realHashes[i].Commitment.Puzzle._Value;
                var p22 = p1.Multiply(Parameters.ServerKey.Encrypt(q)).Mod(Parameters.ServerKey._Key.Modulus);
                if (!p2.Equals(p22))
                    throw new PuzzleException("Invalid quotient");
            }

            _Hashes = _Hashes.OfType<RealHash>().ToArray(); // we do not need the fake one anymore
            InternalState.FakeIndexes = null;
            InternalState.Quotients = proof.Quotients;
            var puzzleToSolve = _Hashes.OfType<RealHash>().First().Commitment.Puzzle;
            BlindFactor blind = null;
            var blindedPuzzle = new Puzzle(Parameters.ServerKey, puzzleToSolve).Blind(ref blind);

            InternalState.BlindFactor = blind;
            InternalState.Status = PromiseClientStates.Completed;
            return blindedPuzzle.PuzzleValue;
        }

        private bool IsValidSignature(PuzzleSolution solution, HashBase hash, out ECDSASignature signature)
        {
            signature = null;
            var escrow = EscrowScriptPubKeyParameters.GetFromCoin(InternalState.EscrowedCoin);
            try
            {
                var key = new XORKey(solution);
                signature = new ECDSASignature(key.XOR(hash.Commitment.Promise));
                var ok = escrow.Initiator.Verify(hash.GetHash(), signature);
                if (!ok)
                    signature = null;
                return ok;
            }
            catch
            {
            }

            return false;
        }


        internal IEnumerable<Transaction> GetSignedTransactions(PuzzleSolution solution)
        {
            if (solution == null)
                throw new ArgumentNullException(nameof(solution));
            AssertState(PromiseClientStates.Completed);
            solution = solution.Unblind(Parameters.ServerKey, InternalState.BlindFactor);
            BigInteger cumul = solution._Value;
            var hashes = _Hashes.OfType<RealHash>().ToArray();
            for (int i = 0; i < Parameters.RealTransactionCount; i++)
            {
                var hash = hashes[i];
                var quotient = i == 0 ? BigInteger.One : InternalState.Quotients[i - 1]._Value;
                cumul = cumul.Multiply(quotient).Mod(Parameters.ServerKey._Key.Modulus);
                solution = new PuzzleSolution(cumul);
                if (!IsValidSignature(solution, hash, out ECDSASignature tumblerSig))
                    continue;
                var transaction = hash.GetTransaction();
                var bobSig = transaction.SignInput(_network, InternalState.EscrowKey, InternalState.EscrowedCoin);
                transaction.Inputs[0].WitScript = new WitScript(
                    Op.GetPushOp(new TransactionSignature(tumblerSig, SigHash.All).ToBytes()),
                    Op.GetPushOp(bobSig.ToBytes()),
                    Op.GetPushOp(InternalState.EscrowedCoin.Redeem.ToBytes())
                );
                //transaction is already witnessified
                if (transaction.Inputs.AsIndexedInputs().First().VerifyScript(_network, InternalState.EscrowedCoin))
                    yield return transaction;
            }
        }

        public Transaction GetSignedTransaction(PuzzleSolution solution)
        {
            var tx = GetSignedTransactions(solution).FirstOrDefault();
            if (tx == null)
                throw new PuzzleException("Wrong solution for the puzzle");
            return tx;
        }

        protected new State InternalState
        {
            get { return (State) base.InternalState; }
            set { base.InternalState = value; }
        }

        public PromiseClientStates Status
        {
            get { return InternalState.Status; }
        }

        private void AssertState(PromiseClientStates state)
        {
            if (state != InternalState.Status)
                throw new InvalidStateException("Invalid state, actual " + InternalState.Status +
                                                " while expected is " + state);
        }
    }
}