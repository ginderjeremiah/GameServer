FROM nginx:alpine

# Copy to the default server block template directory
COPY nginx.conf /etc/nginx/templates/default.conf.template