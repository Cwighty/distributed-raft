FROM nginx:latest as base
COPY ./nginx.conf /etc/nginx/conf.d/default.conf