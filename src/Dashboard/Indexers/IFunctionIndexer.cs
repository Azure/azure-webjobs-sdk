﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Azure.Jobs.Protocols;

namespace Dashboard.Indexers
{
    public interface IFunctionIndexer
    {
        void ProcessFunctionStarted(FunctionStartedMessage message);

        void ProcessFunctionCompleted(FunctionCompletedMessage message);

        void UpgradeData();
    }
}
