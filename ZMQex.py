#
#   Hello World server in Python
#   Binds REP socket to tcp://*:5555
#   Expects b"Hello" from client, replies with b"World"
#

import time
import zmq
import json
import math 

context = zmq.Context()
socket = context.socket(zmq.REP)
socket.bind("tcp://*:5555")

while True:
    #  Wait for next request from client
    message = socket.recv()
    print(f"Received request: {message}")
    
    # Unpack the JSON message
    data = json.loads(message)
    overall = data.get('overall')
    spec = data.get('spec')
    print(f"Overall: {overall}, Spec: {spec}")

    # Recalculate the overall value by summing spec
    overall_dB = 10 * math.log10(sum([x**2 for x in spec])/0.00002/0.00002)

    # Calculate the overall of spec and compare with 60 dB
    if overall_dB > 60:
        print("Overall is greater than 60 dB")
    else:
        print("Overall is less than or equal to 60 dB")

    #  Do some 'work'
    time.sleep(1)

    #  Send reply back to client
    socket.send_string(str(overall_dB))
