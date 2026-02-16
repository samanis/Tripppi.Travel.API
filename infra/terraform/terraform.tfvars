aws_region = "ca-central-1"
environment = "production"
project_name = "airpath"

vpc_cidr = "10.0.0.0/16"
availability_zones = ["ca-central-1a", "ca-central-1b"]

db_instance_class = "db.t4g.micro"
db_name = "airpath"
db_username = "airpathdb"
db_password = "DqYsG0aRTg25pxo3IcBj"
db_multi_az = false

ecs_task_cpu = "512"
ecs_task_memory = "1024"
ecs_desired_count = 1

container_image = ""

allowed_origin = "https://airpath.travel"
origin_secret = "kels19pTMEGrQyJfNHKCWZFYxuXw6gaz"
