# Setting up overlay network
- On node1: `docker swarm init --data-path-port 9191` 
    - the default port wasnt working so make sure to specify the data-path-port
- Copy the command to join the swarm from the output on node2
- On node1 (the manager node): `docker network create --driver=overlay --attachable w-overlay`
    - alternatively you can do this in the docker-compose file

# When installing self hosted runners
- Run the config script
- make sure to add the label of the machine name so the deployment can be targeted to the correct machine   (alex-office4, alex-office5)
