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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenSearch.OpenSearch.Xunit.XunitPlumbing;
using OpenSearch.Net;
using OpenSearch.Client;
using Osc.JsonNetSerializer;
using System.Runtime.Serialization;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using Tests.Framework;
using static Tests.Core.Serialization.SerializationTestHelper;

namespace Tests.ClientConcepts.HighLevel.Serialization
{
	/**[[custom-serialization]]
	 * === Custom Serialization
	 *
	 * After internalizing the serialization routines, and IL-merging the Newtonsoft.Json package, we are pleased to
	 * announce that the next stage of serialization improvements have been completed.
	 *
	 * Both SimpleJson and Newtonsoft.Json have been completely removed and replaced with an implementation of Utf8Json, a fast
	 * serializer that works directly with UTF-8 binary.
	 *
	 * With the move to Utf8Json, we have removed some features that were available in the previous JSON libraries that have
	 * proven too onerous to carry forward at this stage.
	 *
	 * - JSON in the request is never indented, even if SerializationFormatting.Indented is specified. The serialization
	 * routines generated by Utf8Json never generate an IJsonFormatter<T> that will indent JSON, for performance reasons.
	 * We are considering options for exposing indented JSON for development and debugging purposes.
	 *
	 * - OSC types cannot be extended by inheritance. Additional properties can be included for a type by deriving from
	 * that type and annotating these new properties. With the current implementation of serialization with Utf8Json, this approach will not work.
	 *
	 * - Serializer uses Reflection.Emit. Utf8Json uses Reflection.Emit to generate efficient formatters for serializing types that it sees.
	 * Reflection.Emit is not supported on all platforms, for example, UWP, Xamarin.iOS, and Xamarin.Android.
	 *
	 * - OpenSearch.Net.DynamicResponse deserializes JSON arrays to List<object>. SimpleJson deserialized JSON arrays to object[],
	 * but Utf8Json deserializes them to List<object>. This change is preferred for allocation and performance reasons.
	 *
	 * - Utf8Json is much stricter when deserializing JSON object field names to C# POCO properties. With Utf8Json,
	 * JSON object field names must match exactly the name configured for the C# POCO property name.
	 */
	public class GettingStarted
	{
		/**[float]
		 * ==== Injecting a new serializer
		 *
		 * You can inject a serializer that is isolated to only be called for the (de)serialization of `_source`, `_fields`, or
		 * wherever a user provided value is expected to be written and returned.
		 *
		 * Within OSC, we refer to this serializer as the `SourceSerializer`.
		 *
		 * Another serializer also exists within OSC known as the `RequestResponseSerializer`. This serializer is internal
		 * and is responsible for serializing the request and response types that are part of OSC.
		 *
		 * If `SourceSerializer` is left unconfigured, the internal `RequestResponseSerializer` is the `SourceSerializer` as well.
		 *
		 * Implementing `IOpenSearchSerializer` is technically enough to inject your own `SourceSerializer`
		 */
		public class VanillaSerializer : IOpenSearchSerializer
		{
			public T Deserialize<T>(Stream stream) => throw new NotImplementedException();

			public object Deserialize(Type type, Stream stream) => throw new NotImplementedException();

			public Task<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default(CancellationToken)) =>
				throw new NotImplementedException();

			public Task<object> DeserializeAsync(Type type, Stream stream, CancellationToken cancellationToken = default(CancellationToken)) =>
				throw new NotImplementedException();

			public void Serialize<T>(T data, Stream stream, SerializationFormatting formatting = SerializationFormatting.Indented) =>
				throw new NotImplementedException();

			public Task SerializeAsync<T>(T data, Stream stream, SerializationFormatting formatting = SerializationFormatting.Indented,
				CancellationToken cancellationToken = default(CancellationToken)) =>
				throw new NotImplementedException();
		}

		/**
		 * Hooking up the serializer is performed in the `ConnectionSettings` constructor
		 */
		public void TheNewContract()
		{
			var pool = new SingleNodeConnectionPool(new Uri("http://localhost:9200"));
			var connectionSettings = new ConnectionSettings(
				pool,
				sourceSerializer: (builtin, settings) => new VanillaSerializer()); // <1> what the Func?
			var client = new OpenSearchClient(connectionSettings);
		}

		/**
		 * If implementing `IOpenSearchSerializer` is enough, why do we need to provide an instance wrapped in a factory `Func`?
		 *
		 * There are various cases where you might have a POCO type that contains a OSC type as one of its properties. For example,
		 * consider if you want to use percolation; you need to store OpenSearch queries as part of the `_source` of your document,
		 * which means you need to have a POCO that looks something like this
		 */
		public class MyPercolationDocument
		{
			public QueryContainer Query { get; set; }
			public string Category { get; set; }
		}
		/**
		 * A custom serializer would not know how to serialize `QueryContainer` or other OSC types that could appear as part of
		 * the `_source` of a document, therefore a custom serializer needs to have a way to delegate serialization of OSC types
		 * back to OSC's built-in serializer.
		 */

		/**
		 * ==== JsonNetSerializer
		 *
		 * We ship a separate {nuget}/OSC.JsonNetSerializer[OSC.JsonNetSerializer] package that helps in composing a custom `SourceSerializer`
		 * using `Json.NET`, that is smart enough to delegate the serialization of known OSC types back to the built-in
		 * `RequestResponseSerializer`. This package is also useful if
		 *
		 * . You want to control how your documents and values are stored and retrieved from OpenSearch using `Json.NET`
		 * . You want to use `Newtonsoft.Json.Linq` types such as `JObject` within your documents
		 *
		 * The easiest way to hook this custom source serializer up is as follows
		 */
		public void DefaultJsonNetSerializer()
		{
			var pool = new SingleNodeConnectionPool(new Uri("http://localhost:9200"));
			var connectionSettings =
				new ConnectionSettings(pool, sourceSerializer: JsonNetSerializer.Default);
			var client = new OpenSearchClient(connectionSettings);
		}
		/**
		 * `JsonNetSerializer.Default` is just syntactic sugar for passing a delegate like
		 */

		public void DefaultJsonNetSerializerUnsugared()
		{
			var pool = new SingleNodeConnectionPool(new Uri("http://localhost:9200"));
			var connectionSettings = new ConnectionSettings(
				pool,
				sourceSerializer: (builtin, settings) => new JsonNetSerializer(builtin, settings));
			var client = new OpenSearchClient(connectionSettings);
		}
		/**
		 * `JsonNetSerializer`'s constructor takes several methods that allow you to control the `JsonSerializerSettings` and modify
		 * the contract resolver from `Json.NET`.
		 */

		public void DefaultJsonNetSerializerFactoryMethods()
		{
			var pool = new SingleNodeConnectionPool(new Uri("http://localhost:9200"));
			var connectionSettings =
				new ConnectionSettings(pool, sourceSerializer: (builtin, settings) => new JsonNetSerializer(
					builtin, settings,
					() => new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include },
					resolver => resolver.NamingStrategy = new SnakeCaseNamingStrategy()
				));
			var client = new OpenSearchClient(connectionSettings);
		}

		/**
		 * ==== Derived serializers
		 *
		 * If you'd like to be more explicit, you can also derive from `ConnectionSettingsAwareSerializerBase`
		 * and override the `CreateJsonSerializerSettings` and `ModifyContractResolver` methods
		 */
		public class MyFirstCustomJsonNetSerializer : ConnectionSettingsAwareSerializerBase
		{
			public MyFirstCustomJsonNetSerializer(IOpenSearchSerializer builtinSerializer, IConnectionSettingsValues connectionSettings)
				: base(builtinSerializer, connectionSettings) { }

			protected override JsonSerializerSettings CreateJsonSerializerSettings() =>
				new JsonSerializerSettings
				{
					NullValueHandling = NullValueHandling.Include
				};

			protected override void ModifyContractResolver(ConnectionSettingsAwareContractResolver resolver) =>
				resolver.NamingStrategy = new SnakeCaseNamingStrategy();
		}
		/**
		 * Using `MyCustomJsonNetSerializer`, we can serialize using
		 *
		 * - a Json.NET `NamingStrategy` that snake cases property names
		 * - `JsonSerializerSettings` that includes `null` properties
		 *
		 * without affecting how OSC's own types are serialized. Furthermore, because this serializer is aware of
		 * the built-in serializer, we can automatically inject a `JsonConverter` to handle
		 * known OSC types that could appear as part of the source, such as the aformentioned `QueryContainer`.
		 *
		 * Let's demonstrate with an example document type
		 */
		public class MyDocument
		{
			public int Id { get; set; }

			public string Name { get; set; }

			public string FilePath { get; set; }

			public int OwnerId { get; set; }

			public IEnumerable<MySubDocument> SubDocuments { get; set; }
		}

		public class MySubDocument
		{
			public string Name { get; set; }
		}

		/**
		 * Hooking up the serializer and using it is as follows
		 */
		[U] public void UsingJsonNetSerializer()
		{
			var pool = new SingleNodeConnectionPool(new Uri("http://localhost:9200"));
			var connectionSettings = new ConnectionSettings(
				pool,
				connection: new InMemoryConnection(), // <1> an _in-memory_ connection is used here for example purposes. In your production application, you would use an `IConnection` implementation that actually sends a request.
				sourceSerializer: (builtin, settings) => new MyFirstCustomJsonNetSerializer(builtin, settings))
				.DefaultIndex("my-index");

			//hide
			connectionSettings.DisableDirectStreaming();

			var client = new OpenSearchClient(connectionSettings);

			/** Now, if we index an instance of our document type */
			var document = new MyDocument
			{
				Id = 1,
				Name = "My first document",
				OwnerId = 2
			};

			var ndexResponse = client.IndexDocument(document);

			/** it serializes to */
			//json
			var expected = new
			{
				id = 1,
				name = "My first document",
				file_path = (string) null,
				owner_id = 2,
				sub_documents = (object) null
			};
			/**
			 * which adheres to the conventions of our configured `MyCustomJsonNetSerializer` serializer.
			 */

			// hide
			Expect(expected, preserveNullInExpected: true).FromRequest(ndexResponse);
		}

		/** ==== Serializing Type Information
		 * Here's another example that implements a custom contract resolver. The custom contract resolver
		 * will include the type name within the serialized JSON for the document, which can be useful when
		 * returning covariant document types within a collection.
		 */
		public class MySecondCustomContractResolver : ConnectionSettingsAwareContractResolver
		{
			public MySecondCustomContractResolver(IConnectionSettingsValues connectionSettings)
				: base(connectionSettings) { }

			protected override JsonContract CreateContract(Type objectType)
			{
				var contract = base.CreateContract(objectType);
				if (contract is JsonContainerContract containerContract)
				{
					if (containerContract.ItemTypeNameHandling == null)
						containerContract.ItemTypeNameHandling = TypeNameHandling.None;
				}

				return contract;
			}
		}

		public class MySecondCustomJsonNetSerializer : ConnectionSettingsAwareSerializerBase
		{
			public MySecondCustomJsonNetSerializer(IOpenSearchSerializer builtinSerializer, IConnectionSettingsValues connectionSettings)
				: base(builtinSerializer, connectionSettings) { }

			protected override JsonSerializerSettings CreateJsonSerializerSettings() =>
				new JsonSerializerSettings
				{
					TypeNameHandling = TypeNameHandling.All,
					NullValueHandling = NullValueHandling.Ignore,
					TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple
				};

			protected override ConnectionSettingsAwareContractResolver CreateContractResolver() =>
				new MySecondCustomContractResolver(ConnectionSettings); // <1> override the contract resolver
		}

		/**
		 * Now, hooking up this serializer
		 */
		[U] public void MySecondJsonNetSerializer()
		{
			var pool = new SingleNodeConnectionPool(new Uri("http://localhost:9200"));
			var connectionSettings = new ConnectionSettings(
					pool,
					connection: new InMemoryConnection(),
					sourceSerializer: (builtin, settings) => new MySecondCustomJsonNetSerializer(builtin, settings))
				.DefaultIndex("my-index");

			//hide
			connectionSettings.DisableDirectStreaming();

			var client = new OpenSearchClient(connectionSettings);

			/** and indexing an instance of our document type */
			var document = new MyDocument
			{
				Id = 1,
				Name = "My first document",
				OwnerId = 2,
				SubDocuments = new []
				{
					new MySubDocument { Name = "my first sub document" },
					new MySubDocument { Name = "my second sub document" },
				}
			};

			var ndexResponse = client.IndexDocument(document);

			/** serializes to */
			//json
			var expected = new JObject
			{
				{ "$type", "Tests.ClientConcepts.HighLevel.Serialization.GettingStarted+MyDocument, Tests" },
				{ "id", 1 },
				{ "name", "My first document" },
				{ "ownerId", 2 },
				{ "subDocuments", new JArray
					{
						new JObject { { "name", "my first sub document" } },
						new JObject { { "name", "my second sub document" } },
					}
				}
			};
			/**
			 * the type information is serialized for the outer `MyDocument` instance, but not for each
			 * `MySubDocument` instance in the `SubDocuments` collection.
			 *
			 * When implementing a custom contract resolver derived from `ConnectionSettingsAwareContractResolver`,
			 * be careful not to change the behaviour of the resolver for OSC types; doing so will result in
			 * unexpected behaviour.
			 *
			 * [WARNING]
			 * --
			 * Per the https://www.newtonsoft.com/json/help/html/T_Newtonsoft_Json_TypeNameHandling.htm[Json.NET documentation on TypeNameHandling],
			 * it should be used with caution when your application deserializes JSON from an external source.
			 * --
			 */

			// hide
			Expect(expected).FromRequest(ndexResponse);
		}
	}
}
