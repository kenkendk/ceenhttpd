Docker setup with Ceen
=========

Although Ceen can run by itself, you can easily put it behind nginx in a Docker setup, and let nginx handle the external connection. This makes nginx act as a kind of load-balancer, where you can have multiple sites hosted behind a single nginx front.

This document explains how to use the files in this folder, to set up an environment where one or more Ceen instances are running in seperated Docker containers, behind an nginx Docker instance.


