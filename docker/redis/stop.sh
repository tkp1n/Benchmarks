#!/usr/bin/env bash

#echo on
set -x

docker stop benchmarks-redis
docker rm benchmarks-redis
