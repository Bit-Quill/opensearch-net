/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*
* Modifications Copyright OpenSearch Contributors. See
* GitHub history for details.
*
*  Licensed to Elasticsearch B.V. under one or more contributor
*  license agreements. See the NOTICE file distributed with
*  this work for additional information regarding copyright
*  ownership. Elasticsearch B.V. licenses this file to you under
*  the Apache License, Version 2.0 (the "License"); you may
*  not use this file except in compliance with the License.
*  You may obtain a copy of the License at
*
* 	http://www.apache.org/licenses/LICENSE-2.0
*
*  Unless required by applicable law or agreed to in writing,
*  software distributed under the License is distributed on an
*  "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
*  KIND, either express or implied.  See the License for the
*  specific language governing permissions and limitations
*  under the License.
*/

using System;
using OpenSearch.Net;
using OpenSearch.Client;
using Tests.Core.ManagedOpenSearch.Clusters;
using Tests.Framework.EndpointTests;
using Tests.Framework.EndpointTests.TestState;

namespace Tests.Indices.IndexManagement.OpenCloseIndex.OpenIndex
{
	public class OpenIndexApiTests
		: ApiIntegrationTestBase<WritableCluster, OpenIndexResponse, IOpenIndexRequest, OpenIndexDescriptor, OpenIndexRequest>
	{
		public OpenIndexApiTests(WritableCluster cluster, EndpointUsage usage) : base(cluster, usage) { }

		protected override bool ExpectIsValid => true;
		protected override int ExpectStatusCode => 200;

		protected override Func<OpenIndexDescriptor, IOpenIndexRequest> Fluent => d => d
			.IgnoreUnavailable();

		protected override HttpMethod HttpMethod => HttpMethod.POST;

		protected override OpenIndexRequest Initializer => new OpenIndexRequest(CallIsolatedValue)
		{
			IgnoreUnavailable = true
		};

		protected override string UrlPath => $"/{CallIsolatedValue}/_open?ignore_unavailable=true";

		protected override void IntegrationSetup(IOpenSearchClient client, CallUniqueValues values)
		{
			foreach (var index in values.Values)
			{
				client.Indices.Create(index);
				client.Cluster.Health(index, h => h.WaitForStatus(WaitForStatus.Yellow));
				client.Indices.Close(index);
			}
		}

		protected override LazyResponses ClientUsage() => Calls(
			(client, f) => client.Indices.Open(CallIsolatedValue, f),
			(client, f) => client.Indices.OpenAsync(CallIsolatedValue, f),
			(client, r) => client.Indices.Open(r),
			(client, r) => client.Indices.OpenAsync(r)
		);

		protected override OpenIndexDescriptor NewDescriptor() => new OpenIndexDescriptor(CallIsolatedValue);
	}
}
