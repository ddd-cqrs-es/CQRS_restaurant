﻿using System;
using System.CodeDom;
using System.Collections;
using System.Runtime.InteropServices;
using System.Threading;
using CQRS_restaurant.Actors;
using CQRS_restaurant.Handlers;
using CQRS_restaurant.Midgets;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace CQRS_restaurant
{
    public class WireUp
    {
        List<QueuedHandler<CookFood>> cooks;
        private List<IMonitor> monitorQueues;

        public void Start()
        {
            var pubsub = new TopicBasedPubsub();
         
            var clock = new AlarmClock(pubsub);
            
            var midgetHouse = new QueuedHandler<IMessage>(new MidgetHouse(pubsub), "midgets");
            midgetHouse.Start();
            pubsub.Subscribe<IMessage>(midgetHouse);

            var cashier = new QueuedHandler<TakePayment>(new Cashier(pubsub), "cashier");
            var assistant = new QueuedHandler<PriceOrder>(new Assistantmanager(pubsub), "assistant");

            var spike = new Handlers.SpikeOrder();

            var rnd = new Random();
            cooks = new List<QueuedHandler<CookFood>>()
            {
                new QueuedHandler<CookFood>(new Cook(pubsub,  rnd.Next(0,1000) ), "cook1"),
                new QueuedHandler<CookFood>(new Cook(pubsub,  rnd.Next(0,1000) ), "cook2"),
                new QueuedHandler<CookFood>(new Cook(pubsub,  rnd.Next(0,1000) ), "cook3"),
                new QueuedHandler<CookFood>(new Cook(pubsub,  rnd.Next(0,1000) ), "cook4"),
                new QueuedHandler<CookFood>(new Cook(pubsub,  rnd.Next(0,1000) ), "cook5"),

            };

            var kitchen = new QueuedHandler<CookFood>(
                new RandomlyDropOrder<CookFood>(new TimeToLiveHandler<CookFood>(
                    new MoreFairDispatcherHandler<CookFood>(cooks)
                    ), 3), "kitchen");

            var printer = new ConsolePrintingHandler();

            pubsub.Subscribe(kitchen);
            pubsub.Subscribe(assistant);
            pubsub.Subscribe(cashier);
            pubsub.Subscribe(spike);
            pubsub.Subscribe(clock);

            kitchen.Start();
            assistant.Start();
            cashier.Start();
            clock.Start();

            monitorQueues = new List<IMonitor>();

            foreach (var threadCook in cooks)
            {
                monitorQueues.Add(threadCook);
                threadCook.Start();
            }

            monitorQueues.Add(kitchen);
            monitorQueues.Add(cashier);
            monitorQueues.Add(assistant);

            var waiter = new Waiter(pubsub);

            var thread = new Thread(Monitor);
            thread.Start();

            var items = new[]
            {
                new Item {Name = "Soup", Qty = 2, Price = 3.50m},
                new Item {Name = "Goulash", Qty = 2, Price = 3.50m}
            };

            for (int i = 0; i < 10; i++)
            {
                var corr = Guid.NewGuid().ToString();
                //pubsub.Subscribe<CookFood>(printer, corr);
                //pubsub.Subscribe<PlaceOrder>(printer, corr);
                //pubsub.Subscribe<TakePayment>(printer, corr);
                waiter.PlaceOrder(items, corr);

            }

            Console.ReadLine();
        }

        public void Monitor()
        {
            while (true)
            {
                Thread.Sleep(1000);
                foreach (var startable in monitorQueues)
                {
                    Console.WriteLine(startable.Status());
                }
            }
        }

    }

    class Program
    {
        private static void Main(string[] args)
        {
            var v = new WireUp();
            v.Start();
        }
    }

    public interface IPublisher
    {
        void Publish<T>(string topic, T message) where T : IMessage;
        void Publish<T>(T message) where T : IMessage;
        void UnSubscribe(string correlationId);
    }

    public class Widener<TIn, TOut> : IHandler<TIn>
        where TOut : TIn
        where TIn : IMessage
    {
        private readonly IHandler<TOut> _handler;

        public Widener(IHandler<TOut> handler)
        {
            _handler = handler;
        }

        public void Handle(TIn message)
        {
            TOut mess;
            try
            {
                mess = (TOut)message;
            }
            catch
            {
                return;
            }
            _handler.Handle(mess);
        }
    }


    public class Item
    {
        public string Name { get; set; }
        public int Qty { get; set; }
        public decimal Price { get; set; }
    }



}


