FROM nginx:alpine

# Copy the template to the folder where alpine automatically performs envsubst
COPY nginx.conf /etc/nginx/templates/nginx.conf.template

# Railway injects the PORT variable automatically
EXPOSE 80