#!/bin/sh
# Start the .NET application in the background
dotnet FeedCord.dll &
# Capture the process ID of the background app
pid=$!
# Wait 60 seconds before restarting
sleep 60
# Notify user about the restart
echo "Restarting the application..."
# Stop the initial run
kill $pid
# Restart the .NET application in the foreground
dotnet FeedCord.dll
