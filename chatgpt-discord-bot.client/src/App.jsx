import React, { useEffect, useState } from 'react';
import { BarChart, Bar, PieChart, Pie, Cell, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import './App.css';

const App = () => {
    const [totalMessages, setTotalMessages] = useState(0);
    const [messagesPerUser, setMessagesPerUser] = useState([]);
    const [messagesPerChannel, setMessagesPerChannel] = useState([]);
    const [serverUptime, setServerUptime] = useState('');

    useEffect(() => {
        // Fetch total messages
        fetch('/api/stats/messages/total')
            .then(response => response.json())
            .then(data => setTotalMessages(data.totalMessages));

        // Fetch messages per user
        fetch('/api/stats/messages/user')
            .then(response => response.json())
            .then(data => {
                const formattedData = Object.entries(data).map(([user, count]) => ({ user, count }));
                setMessagesPerUser(formattedData);
            });

        // Fetch messages per channel
        fetch('/api/stats/messages/channel')
            .then(response => response.json())
            .then(data => {
                const formattedData = Object.entries(data).map(([channel, count]) => ({ channel, count }));
                setMessagesPerChannel(formattedData);
            });

        // Fetch server uptime
        fetch('/api/stats/server/uptime')
            .then(response => response.json())
            .then(data => setServerUptime(data.uptime));
    }, []);

    return (
        <div className="dashboard">
            <h2>Service Status</h2>
            <div className="status-indicators">
                <div className="status-card">
                    <h3>Total Messages</h3>
                    <p>{totalMessages}</p>
                </div>
                <div className="status-card">
                    <h3>Server Uptime</h3>
                    <p>{serverUptime}</p>
                </div>
            </div>

            <h3>Messages per User</h3>
            <ResponsiveContainer width="100%" height={400}>
                <BarChart data={messagesPerUser}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="user" />
                    <YAxis />
                    <Tooltip />
                    <Bar dataKey="count" fill="#8884d8" />
                </BarChart>
            </ResponsiveContainer>

            <h3>Messages per Channel</h3>
            <ResponsiveContainer width="100%" height={400}>
                <PieChart>
                    <Pie
                        data={messagesPerChannel}
                        dataKey="count"
                        nameKey="channel"
                        outerRadius={150}
                        fill="#8884d8"
                        label
                    >
                        {messagesPerChannel.map((entry, index) => (
                            <Cell key={`cell-${index}`} fill={index % 2 === 0 ? '#8884d8' : '#82ca9d'} />
                        ))}
                    </Pie>
                    <Tooltip />
                </PieChart>
            </ResponsiveContainer>
        </div>
    );
};

export default App;