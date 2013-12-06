﻿using System;
using System.Configuration;
using System.Reflection;
using Cronus.Core;
using NMSD.Cronus.Core.Commanding;
using NMSD.Cronus.Core.Cqrs;
using NMSD.Cronus.Core.Eventing;
using NMSD.Cronus.Core.EventStoreEngine;
using NMSD.Cronus.Core.Messaging;
using NMSD.Cronus.Sample.Collaboration.Collaborators;
using NMSD.Cronus.Sample.Collaboration.Collaborators.Events;
using NMSD.Cronus.Sample.Collaboration.Projections;
using Protoreg;

namespace NMSD.Cronus.Sample.ApplicationService
{
    class Program
    {
        static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();

            var protoRegistration = new ProtoRegistration();
            protoRegistration.RegisterAssembly<CollaboratorState>();
            protoRegistration.RegisterAssembly<NewCollaboratorCreated>();
            protoRegistration.RegisterAssembly<Wraper>();
            //protoRegistration.RegisterCommonType(typeof(AggregateRootState<CollaboratorId>));
            ProtoregSerializer serializer = new ProtoregSerializer(protoRegistration);
            serializer.Build();

            var eventConsumer = new RabbitEventConsumer(serializer);
            eventConsumer.RegisterAllHandlersInAssembly(Assembly.GetAssembly(typeof(CollaboratorProjection)));
            eventConsumer.Start(2);

            var systemConsumer = new RabbitSystemConsumer(serializer);
            string connectionString = ConfigurationManager.ConnectionStrings["cronus-es"].ConnectionString;
            var eventStore = new InMemoryEventStore(connectionString, serializer);
            systemConsumer.RegisterHandler(typeof(MessageCommit), typeof(SystemHandler), (x) =>
            {
                var handler = new SystemHandler();
                handler.EventStore = eventStore;
                return handler;
            });
            systemConsumer.Start(2);

            var eventPublisher = new RabbitSystemPublisher(serializer);




            var commandConsumer = new RabbitCommandConsumer(serializer);
            commandConsumer.RegisterAllHandlersInAssembly(type =>
                                                                {
                                                                    var handler = FastActivator.CreateInstance(type) as IAggregateRootApplicationService;
                                                                    handler.EventPublisher = eventPublisher;
                                                                    return handler;
                                                                },
                                                                Assembly.GetAssembly(typeof(CollaboratorAppService)));
            commandConsumer.Start(4);

            Console.ReadLine();
        }
    }
}