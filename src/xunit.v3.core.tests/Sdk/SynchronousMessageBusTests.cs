﻿using System.Collections.Generic;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

public class SynchronousMessageBusTests
{
	[Fact]
	public void MessagesAreDispatchedImmediatelyFromBus()
	{
		var msg1 = new _MessageSinkMessage();
		var dispatchedMessages = new List<_MessageSinkMessage>();

		using (var bus = new SynchronousMessageBus(SpyMessageSink.Create(messages: dispatchedMessages)))
			Assert.True(bus.QueueMessage(msg1));

		Assert.Collection(dispatchedMessages, message => Assert.Same(msg1, message));
	}

	[Fact]
	public void BusShouldReportShutdownWhenMessageSinkReturnsFalse()
	{
		using var bus = new SynchronousMessageBus(SpyMessageSink.Create(returnResult: false));

		Assert.False(bus.QueueMessage(new _MessageSinkMessage()));
	}
}
