﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using Xunit;
using Xunit.Runner.Common;
using Xunit.Runner.v2;
using Xunit.Sdk;
using Xunit.v3;

public class SerializationTests
{
	[Serializable]
	class SerializableObject { }

	[Fact]
	public static void CanSerializeAndDeserializeObjectsInATest()
	{
		var bf = new BinaryFormatter();
		using var ms = new MemoryStream();

		bf.Serialize(ms, new SerializableObject());
		ms.Position = 0;
		var o = bf.Deserialize(ms);

		Assert.IsType(typeof(SerializableObject), o);
		var o2 = (SerializableObject)o;  // Should not throw
	}

	[Fact]
	public static void SerializedTestsInSameCollectionRemainInSameCollection()
	{
		var assemblyInfo = Reflector.Wrap(Assembly.GetExecutingAssembly());
		var discoverer = new XunitTestFrameworkDiscoverer(assemblyInfo, configFileName: null, _NullSourceInformationProvider.Instance, SpyMessageSink.Create());
		var sink = new TestDiscoverySink();

		discoverer.Find(typeof(ClassWithFacts).FullName!, sink, _TestFrameworkOptions.ForDiscovery());
		sink.Finished.WaitOne();

		var first = sink.TestCases[0];
		var second = sink.TestCases[1];
		Assert.NotEqual(first.UniqueID, second.UniqueID);

		Assert.True(TestCollectionComparer.Instance.Equals(first.TestMethod.TestClass.TestCollection, second.TestMethod.TestClass.TestCollection));

		var serializedFirst = SerializationHelper.Deserialize<_ITestCase>(SerializationHelper.Serialize(first));
		var serializedSecond = SerializationHelper.Deserialize<_ITestCase>(SerializationHelper.Serialize(second));

		Assert.NotNull(serializedFirst);
		Assert.NotNull(serializedSecond);
		Assert.NotSame(serializedFirst.TestMethod.TestClass.TestCollection, serializedSecond.TestMethod.TestClass.TestCollection);
		Assert.True(TestCollectionComparer.Instance.Equals(serializedFirst.TestMethod.TestClass.TestCollection, serializedSecond.TestMethod.TestClass.TestCollection));
	}

	class ClassWithFacts
	{
		[Fact]
		public void Test1() { }

		[Fact]
		public void Test2() { }
	}

	[Fact]
	public static void TheoriesWithSerializableData_ReturnAsIndividualTestCases()
	{
		var assemblyInfo = Reflector.Wrap(Assembly.GetExecutingAssembly());
		var discoverer = new XunitTestFrameworkDiscoverer(assemblyInfo, configFileName: null, _NullSourceInformationProvider.Instance, SpyMessageSink.Create());
		var sink = new TestDiscoverySink();

		discoverer.Find(typeof(ClassWithTheory).FullName!, sink, _TestFrameworkOptions.ForDiscovery());
		sink.Finished.WaitOne();

		var first = sink.TestCases[0];
		var second = sink.TestCases[1];
		Assert.NotEqual(first.UniqueID, second.UniqueID);

		Assert.True(TestCollectionComparer.Instance.Equals(first.TestMethod.TestClass.TestCollection, second.TestMethod.TestClass.TestCollection));

		var serializedFirst = SerializationHelper.Deserialize<_ITestCase>(SerializationHelper.Serialize(first));
		var serializedSecond = SerializationHelper.Deserialize<_ITestCase>(SerializationHelper.Serialize(second));

		Assert.NotNull(serializedFirst);
		Assert.NotNull(serializedSecond);
		Assert.NotSame(serializedFirst.TestMethod.TestClass.TestCollection, serializedSecond.TestMethod.TestClass.TestCollection);
		Assert.True(TestCollectionComparer.Instance.Equals(serializedFirst.TestMethod.TestClass.TestCollection, serializedSecond.TestMethod.TestClass.TestCollection));
	}

	class ClassWithTheory
	{
		[Theory]
		[InlineData(1)]
		[InlineData("hello")]
		public void Test(object x) { }
	}

	[Fact]
	public static void TheoryWithNonSerializableData_ReturnsAsASingleTestCase()
	{
		var assemblyInfo = Reflector.Wrap(Assembly.GetExecutingAssembly());
		var discoverer = new XunitTestFrameworkDiscoverer(assemblyInfo, configFileName: null, _NullSourceInformationProvider.Instance, SpyMessageSink.Create());
		var sink = new TestDiscoverySink();

		discoverer.Find(typeof(ClassWithNonSerializableTheoryData).FullName!, sink, _TestFrameworkOptions.ForDiscovery());
		sink.Finished.WaitOne();

		var testCase = Assert.Single(sink.TestCases);
		Assert.IsType<XunitTheoryTestCase>(testCase);

		var deserialized = SerializationHelper.Deserialize<_ITestCase>(SerializationHelper.Serialize(testCase));
		Assert.IsType<XunitTheoryTestCase>(deserialized);
	}

	class ClassWithNonSerializableTheoryData
	{
		public static IEnumerable<object[]> Data = new[] { new[] { new object() }, new[] { new object() } };

		[Theory]
		[MemberData("Data")]
		public void Test(object x) { }
	}
}
