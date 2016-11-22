# This file is part of Buildbot.  Buildbot is free software: you can
# redistribute it and/or modify it under the terms of the GNU General Public
# License as published by the Free Software Foundation, version 2.
#
# This program is distributed in the hope that it will be useful, but WITHOUT
# ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
# FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more
# details.
#
# You should have received a copy of the GNU General Public License along with
# this program; if not, write to the Free Software Foundation, Inc., 51
# Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.
#
# Copyright Buildbot Team Members


"""Push messages to rabbitmq

."""

from buildbot import config
from kombu import Connection, Exchange
from twisted.python import log
from twisted.python.failure import Failure

from buildbot.status.status_queue import QueuedStatusPush


class RabbitMQStatusPush(QueuedStatusPush):
    """Event streamer to a RabbitMQ server."""

    def __init__(self, amqpUri, exchangeName, exchangeType, durable=True, **kwargs):
        """
        :param amqpUri: The RabbitMQ server to be used to push events notifications to.
        :param exchangeName: RabbitMQ exchange name
        :param exchangeType: RabbitMQ exchange name
        :param durable: RabbitMQ exchange durability
        :param kwargs: QueuedStatusPush kwargs
        """
        if not amqpUri:
            raise config.ConfigErrors(['RabbitMQStatusPush requires a amqp URI'])

        # Parameters.
        self.serverUrl = amqpUri
        self.exchange = Exchange(name=exchangeName, type=exchangeType, durable=durable)

        # Use the unbounded method.
        QueuedStatusPush.__init__(self, **kwargs)

    def pushData(self, packets, items):
        """
        :param packets:
        :param items: list of dict containing the messages
        :return:
        """
        try:
            with Connection('amqp://{}//'.format(self.serverUrl)) as conn:
                producer = conn.Producer()
                for item in items:
                    producer.publish(item, exchange=self.exchange)

            return True, None

        except Exception as e:
            log.err(Failure(), "RabbitMQStatusPush.pushData failed")
            return False, e
