/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/
/*
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

using System.Linq;
using OpenSearch.OpenSearch.Xunit.XunitPlumbing;
using FluentAssertions;
using OpenSearch.Client;
using OpenSearch.Client.Specification.ClusterApi;
using Tests.Core.Client;

namespace Tests.Reproduce
{
	public class GithubIssue3210
	{
		private const string ClusterAllocationResponse = @"{
  ""index"" : ""idx"",
	""shard"" : 0,
	""primary"" : true,
	""current_state"" : ""unassigned"",
	""unassigned_info"" : {
		""reason"" : ""INDEX_CREATED"",
		""at"" : ""2017-01-04T18:08:16.600Z"",
		""last_allocation_status"" : ""no""
	},
	""can_allocate"" : ""no"",
	""allocate_explanation"" : ""cannot allocate because allocation is not permitted to any of the nodes"",
	""node_allocation_decisions"" : [
		{
			""node_id"": ""_3APDoyWQUGJC2J_LbxQVw"",
			""node_name"": ""node"",
			""transport_address"": ""10.10.10.10:9300"",
			""node_attributes"": {
				""rack_id"": ""2"",
				""machinetype"": ""warm""
			},
			""node_decision"": ""worse_balance"",
			""weight_ranking"": 4
		}
	]
}";

		[U] public void MissingNodeDecisionOptionsInResponseThrowExceptionWhenAttemptingToDeserializeResponse()
		{
			var client = FixedResponseClient.Create(ClusterAllocationResponse);
			var response = client.Cluster.AllocationExplain();

			var nodeAllocationDecisions = response.NodeAllocationDecisions;
			nodeAllocationDecisions.Should().NotBeNullOrEmpty();
			nodeAllocationDecisions.First().NodeDecision.Should().NotBeNull().And.Be(Decision.WorseBalance);
		}
	}
}
