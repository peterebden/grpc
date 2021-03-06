#region Copyright notice and license

// Copyright 2015 gRPC authors.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Grpc.Core.Logging;
using Grpc.Core.Utils;

namespace Grpc.Core.Internal
{
    internal delegate void BatchCompletionDelegate(bool success, BatchContextSafeHandle ctx);

    internal delegate void RequestCallCompletionDelegate(bool success, RequestCallContextSafeHandle ctx);

    internal class CompletionRegistry
    {
        static readonly ILogger Logger = GrpcEnvironment.Logger.ForType<CompletionRegistry>();

        readonly GrpcEnvironment environment;
        readonly Dictionary<IntPtr, IOpCompletionCallback> dict = new Dictionary<IntPtr, IOpCompletionCallback>(new IntPtrComparer());
        readonly object myLock = new object();
        IntPtr lastRegisteredKey;  // only for testing

        public CompletionRegistry(GrpcEnvironment environment)
        {
            this.environment = environment;
        }

        public void Register(IntPtr key, IOpCompletionCallback callback)
        {
            environment.DebugStats.PendingBatchCompletions.Increment();
            lock (myLock)
            {
                dict.Add(key, callback);
                this.lastRegisteredKey = key;
            }
        }

        public void RegisterBatchCompletion(BatchContextSafeHandle ctx, BatchCompletionDelegate callback)
        {
            ctx.CompletionCallback = callback;
            Register(ctx.Handle, ctx);
        }

        public void RegisterRequestCallCompletion(RequestCallContextSafeHandle ctx, RequestCallCompletionDelegate callback)
        {
            ctx.CompletionCallback = callback;
            Register(ctx.Handle, ctx);
        }

        public IOpCompletionCallback Extract(IntPtr key)
        {
            IOpCompletionCallback value = null;
            lock (myLock)
            {
                value = dict[key];
                dict.Remove(key);
            }
            environment.DebugStats.PendingBatchCompletions.Decrement();
            return value;
        }

        /// <summary>
        /// For testing purposes only. NOT threadsafe.
        /// </summary>
        public IntPtr LastRegisteredKey
        {
            get { return this.lastRegisteredKey; }
        }

        /// <summary>
        /// IntPtr doesn't implement <c>IEquatable{IntPtr}</c> so we need to use custom comparer to avoid boxing.
        /// </summary>
        private class IntPtrComparer : IEqualityComparer<IntPtr>
        {
            public bool Equals(IntPtr x, IntPtr y)
            {
                return x == y;
            }

            public int GetHashCode(IntPtr obj)
            {
                return obj.GetHashCode();
            }
        }
    }
}
