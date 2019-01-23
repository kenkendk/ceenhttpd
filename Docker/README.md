Docker setup with Ceen
=========

Although Ceen can run by itself, you can easily put it behind nginx in a Docker setup, and let nginx handle the external connection. This makes nginx act as a kind of load-balancer, where you can have multiple sites hosted behind a single nginx front.

The `compose.yml` document has an example for how to set up an environment where one or more Ceen instances are running in seperated Docker containers, behind an nginx Docker instance.

The `compose.yml` file shows how to run Ceen with both Mono and .Net Core. The examples assume that you have a `ceen-mono` or `ceen-netcore` folder with the compiled application inside. When yuou start the docker container, it will mount this folder inside the image.

This approach allows you to run your application within the Docker environment, but having the files on the host system for easy on-the-fly updatinh.

You can also create pre-baked containers with the compiles binaries inside, which is prefered if you are using a container orchestration tool to deply your containers.


