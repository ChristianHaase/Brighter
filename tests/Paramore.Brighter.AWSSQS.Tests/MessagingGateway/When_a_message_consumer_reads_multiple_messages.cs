﻿using System;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using FluentAssertions;
using Paramore.Brighter.AWSSQS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWSSQS.Tests.MessagingGateway
{
    [Collection("AWS")]
    [Trait("Category", "AWS")]
    public class SQSBufferedConsumerTests : IDisposable
    {
        private readonly SqsMessageProducer _messageProducer;
        private SqsMessageConsumer _consumer;
        private readonly string _topicName = Guid.NewGuid().ToString().ToValidSNSTopicName();
        private ChannelFactory _channelFactory;
        private const string CONTENT_TYPE = "text\\plain";
        private const int BUFFER_SIZE = 3;
        private const int MESSAGE_COUNT = 4;

        public SQSBufferedConsumerTests()
        {
            //Must have credentials stored in the SDK Credentials store or shared credentials file
            var credentialChain = new CredentialProfileStoreChain();
            
            (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
            var awsConnection = new AWSMessagingGatewayConnection(credentials, region);

            _channelFactory = new ChannelFactory(awsConnection, new SqsMessageConsumerFactory(awsConnection));
            var channelName = $"Buffered-Consumer-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            _topicName = $"Buffered-Consumer-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
                
            //we need the channel to create the queues and notifications
            _channelFactory.CreateChannel(new Connection<MyCommand>(
                name: new ConnectionName(channelName),
                channelName:new ChannelName(channelName),
                routingKey:new RoutingKey(_topicName),
                bufferSize: BUFFER_SIZE
                ));
            
            //we want to access via a consumer, to receive multiple messages - we don't want to expose on channel
            //just for the tests, so create a new consumer from the properties
            var sqsQueueName = new ChannelName(channelName).ToValidSQSQueueName();
            _consumer = new SqsMessageConsumer(awsConnection, sqsQueueName, BUFFER_SIZE);
           _messageProducer = new SqsMessageProducer(awsConnection);
        }
            
        [Fact]
        public void When_a_message_consumer_reads_multiple_messages()
        {
            var messageOne = new Message(
                new MessageHeader(Guid.NewGuid(), _topicName, MessageType.MT_COMMAND, Guid.NewGuid(), string.Empty, CONTENT_TYPE),
                new MessageBody("test content one")
                );
            
            var messageTwo= new Message(
                new MessageHeader(Guid.NewGuid(), _topicName, MessageType.MT_COMMAND, Guid.NewGuid(), string.Empty, CONTENT_TYPE),
                new MessageBody("test content two")
                );
           
            var messageThree= new Message(
                new MessageHeader(Guid.NewGuid(), _topicName, MessageType.MT_COMMAND, Guid.NewGuid(), string.Empty, CONTENT_TYPE),
                new MessageBody("test content three")
                );
             
            var messageFour= new Message(
                new MessageHeader(Guid.NewGuid(), _topicName, MessageType.MT_COMMAND, Guid.NewGuid(), string.Empty, CONTENT_TYPE),
                new MessageBody("test content four")
                );
             
            //send MESSAGE_COUNT messages 
            _messageProducer.Send(messageOne);
            _messageProducer.Send(messageTwo);
            _messageProducer.Send(messageThree);
            _messageProducer.Send(messageFour);


            int messagesRecieved = 0;
            do
            {
                var outstandingMessageCount = MESSAGE_COUNT - messagesRecieved;

                //retrieve  messages
                var moreMessages = _consumer.Receive(1000);
                
                moreMessages.Length.Should().BeLessOrEqualTo(outstandingMessageCount);
                
                //should not receive more than buffer in one hit
                moreMessages.Length.Should().BeLessOrEqualTo(BUFFER_SIZE);

                foreach (var message in moreMessages)
                {
                    //this will be MT_NONE for an empty message
                    message.Header.MessageType.Should().Be(MessageType.MT_COMMAND);
                    _consumer.Acknowledge(message);
                }

                messagesRecieved += moreMessages.Length;
            } while (messagesRecieved < MESSAGE_COUNT);

        }
        
        public void Dispose()
        {
            //Clean up resources that we have created
            _channelFactory.DeleteTopic();
            _channelFactory.DeleteQueue();
        }
    }
}


